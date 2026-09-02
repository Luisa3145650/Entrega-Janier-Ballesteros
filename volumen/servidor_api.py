import sys
sys.stdout.reconfigure(encoding='utf-8')
sys.stderr.reconfigure(encoding='utf-8')

from flask import Flask, jsonify, Response, request
from flask_cors import CORS
import cv2
import numpy as np
import serial
import serial.tools.list_ports
import re
from collections import deque
import threading
import time
import os
import json
import math
import traceback
import subprocess
from typing import Optional, Tuple

app = Flask(__name__)
CORS(app)

# ==========================================
# CONFIGURACIÓN DEL SISTEMA Y PERSISTENCIA
# ==========================================
PIXELES_POR_CM = float(os.environ.get("PIXELES_POR_CM", "39.5"))
DENSIDAD_HUEVO = 1.07
FACTOR_FORMA = 0.517
BAUDRATE = 9600
INTERVALO_INFERENCIA_SEG = float(os.environ.get("YOLO_INFERENCE_INTERVAL_SEC", "0.1"))

# Configuración morfológica para filtrado HSV (combate parpadeo LED)
KERNEL_OPEN_SIZE = int(os.environ.get("HSV_KERNEL_OPEN", "9"))
KERNEL_CLOSE_SIZE = int(os.environ.get("HSV_KERNEL_CLOSE", "17"))

# Configuración avanzada anti-flicker (rolling shutter / tiras LED) y cámara
CAMARA_EXPOSURE_MANUAL = os.environ.get("CAMARA_EXPOSURE_MANUAL", "1") == "1"
CAMARA_EXPOSURE_VALOR = float(os.environ.get("CAMARA_EXPOSURE_VALOR", "-6"))
CAMARA_GAIN_MANUAL = os.environ.get("CAMARA_GAIN_MANUAL", "1") == "1"
CAMARA_GAIN_VALOR = float(os.environ.get("CAMARA_GAIN_VALOR", "0"))
CAMARA_AUTO_WB = os.environ.get("CAMARA_AUTO_WB", "0") == "1"
CAMARA_AUTOFOCUS = os.environ.get("CAMARA_AUTOFOCUS", "0") == "1"
CAMARA_FPS = float(os.environ.get("CAMARA_FPS", "30.0"))

CONFIG_DIR = os.path.join(os.environ.get('PROGRAMDATA', 'C:\\ProgramData'), 'ClasificadorHuevos')
CONFIG_FILE = os.path.join(CONFIG_DIR, 'config.json')
LOG_FILE = os.path.join(CONFIG_DIR, 'debug_python.log')

def log_debug(mensaje: str):
    """Escribe un mensaje de diagnóstico con timestamp en C:\\ProgramData\\ClasificadorHuevos\\debug_python.log"""
    try:
        if not os.path.exists(CONFIG_DIR):
            os.makedirs(CONFIG_DIR, exist_ok=True)
        timestamp = time.strftime("%Y-%m-%d %H:%M:%S")
        linea = f"[{timestamp}] {mensaje}\n"
        print(mensaje)
        with open(LOG_FILE, 'a', encoding='utf-8') as f:
            f.write(linea)
    except Exception as ex_log:
        print(f"Error escribiendo en log: {ex_log}")

PUERTO_BASCULA = ''
CAMARA_INDEX = 0
CAMARA_NOMBRE = ''
CONFIGURADO = False

# Lock exclusivo para hardware (Cámara y Báscula serial USB)
lock_hardware = threading.Lock()
pausa_vision = False
vision_corriendo = True

bascula = None
cap = None

# Lock exclusivo para compartir frames e inferencias de IA de forma asíncrona
lock_inferencia = threading.Lock()
frame_para_inferencia = None
ultima_deteccion = (False, None, None, None, "Inicializando")

# Lock exclusivo para el buffer de streaming HTTP /frame.jpg
lock_frame = threading.Lock()
frame_actual = None

# ==========================================
# MODELO IA YOLOV8-SEG (DISPOSITIVO CONFIGURABLE + FALLBACK)
# ==========================================
DEVICE_YOLO = os.environ.get("YOLO_DEVICE", "cpu")
RUTA_MODELO_YOLO = os.path.join(os.path.dirname(__file__), "modelo", "best.pt")
modelo_yolo = None

try:
    if os.path.exists(RUTA_MODELO_YOLO):
        log_debug(f"ℹ️ Intentando cargar YOLOv8-seg desde: {RUTA_MODELO_YOLO}")
        from ultralytics import YOLO
        modelo_yolo = YOLO(RUTA_MODELO_YOLO)
        try:
            modelo_yolo.to(DEVICE_YOLO)
        except Exception as ex_dev:
            log_debug(f"ℹ️ Aviso al asignar device {DEVICE_YOLO}: {ex_dev}")
        _dummy = np.zeros((480, 640, 3), dtype=np.uint8)
        _ = modelo_yolo(_dummy, verbose=False, device=DEVICE_YOLO)
        log_debug(f"✅ Modelo YOLOv8-seg cargado exitosamente en device='{DEVICE_YOLO}' desde: {RUTA_MODELO_YOLO}")
    else:
        log_debug(f"⚠️ No se encontró el modelo en {RUTA_MODELO_YOLO}. Se usará fallback HSV.")
except Exception as ex_yolo_load:
    modelo_yolo = None
    tb_str = traceback.format_exc()
    log_debug(f"⚠️ Error cargando YOLOv8-seg ({ex_yolo_load}). Se usará fallback HSV.\nTraceback completo:\n{tb_str}")

datos_globales = {
    "largo": 0.0,
    "ancho": 0.0,
    "peso": 0.0,
    "elipsoide": 0.0,
    "revolucion": 0.0,
    "bascula": 0.0,
    "volumen_real": 0.0,
    "categoria": "ESPERANDO...",
    "metodo_deteccion": "YOLOv8-seg" if modelo_yolo is not None else "Fallback-HSV",
    "huevo_detectado": False,
    "es_valido": False
}

historial_largo = deque(maxlen=5)
historial_ancho = deque(maxlen=5)
historial_peso = deque(maxlen=2)
historial_volumen = deque(maxlen=12)

peso_actual_memoria = 0.0
ultimo_timestamp_serial = time.time()
ciclos_sin_datos = 0


# ==========================================
# FÓRMULAS DE VOLUMEN Y CLASIFICACIÓN (NTC 1240)
# ==========================================
def volumen_elipsoide(largo_cm: float, diametro_cm: float) -> float:
    a, b = largo_cm / 2.0, diametro_cm / 2.0
    return (4.0 / 3.0) * math.pi * a * (b ** 2)


def volumen_por_contorno(mascara_px: np.ndarray, px_por_cm: float, n_discos: int = 60) -> Optional[float]:
    """
    Cálculo del volumen por integración del contorno real mediante discos infinitesimales
    (Teorema de Pappus / Sólido de Revolución). Portado de modelo copia/server.py.
    """
    if mascara_px is None or px_por_cm <= 0:
        return None
    try:
        contornos, _ = cv2.findContours(mascara_px.astype(np.uint8), cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_NONE)
        if not contornos:
            return None
        contorno = max(contornos, key=cv2.contourArea).reshape(-1, 2).astype(np.float32)

        centro = contorno.mean(axis=0)
        pts_c = contorno - centro
        cov = np.cov(pts_c.T)
        valores, vectores = np.linalg.eigh(cov)
        eje_mayor = vectores[:, np.argmax(valores)]
        eje_mayor = eje_mayor / np.linalg.norm(eje_mayor)
        eje_menor = np.array([-eje_mayor[1], eje_mayor[0]])

        proy_mayor = pts_c @ eje_mayor
        proy_menor = pts_c @ eje_menor

        largo_px = proy_mayor.max() - proy_mayor.min()
        if largo_px <= 0:
            return None

        inicio = proy_mayor.min()
        paso = largo_px / n_discos
        volumen_px3 = 0.0
        for i in range(n_discos):
            lo, hi = inicio + i * paso, inicio + (i + 1) * paso
            mascara_franja = (proy_mayor >= lo) & (proy_mayor < hi)
            if not np.any(mascara_franja):
                continue
            radio_px = np.max(np.abs(proy_menor[mascara_franja]))
            volumen_px3 += math.pi * (radio_px ** 2) * paso

        return volumen_px3 / (px_por_cm ** 3)
    except Exception:
        return None


def volumen_narushin(largo_cm: float, diametro_cm: float) -> float:
    return 0.51 * largo_cm * (diametro_cm ** 2)


def clasificar_huevo(peso_g: float) -> str:
    """Clasificación según norma NTC 1240:2011 / FENAVI"""
    if peso_g < 45.0: return "TIPO C"
    elif 45.0 <= peso_g < 53.0: return "TIPO B"
    elif 53.0 <= peso_g < 60.0: return "TIPO A"
    elif 60.0 <= peso_g < 67.0: return "TIPO AA"
    elif 67.0 <= peso_g < 78.0: return "TIPO AAA"
    else: return "JUMBO"


# ==========================================
# CALIBRACIÓN DE ESCALA POR CUADRÍCULA (1 cm x 1 cm)
# ==========================================
historial_calibracion_px = deque(maxlen=20)
ultimo_check_calibracion = 0.0

def calibrar_pixeles_desde_cuadricula(frame: np.ndarray, mascara_huevo: Optional[np.ndarray] = None) -> float:
    """
    Detecta las líneas de la cuadrícula de 1cm x 1cm en el tapete verde usando cv2.HoughLinesP.
    Calcula el espaciado medio entre líneas paralelas para calibrar PIXELES_POR_CM dinámicamente.
    """
    global PIXELES_POR_CM, historial_calibracion_px
    try:
        roi = frame.copy()
        if mascara_huevo is not None:
            kernel_dil = cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (30, 30))
            masc_ampliada = cv2.dilate(mascara_huevo, kernel_dil, iterations=1)
            roi[masc_ampliada > 0] = [0, 0, 0]

        gris = cv2.cvtColor(roi, cv2.COLOR_BGR2GRAY)
        gris_suave = cv2.bilateralFilter(gris, 7, 50, 50)
        bordes = cv2.Canny(gris_suave, 40, 130, apertureSize=3)

        lineas = cv2.HoughLinesP(bordes, rho=1, theta=np.pi/180, threshold=65, minLineLength=45, maxLineGap=15)
        if lineas is not None and len(lineas) >= 4:
            pos_horizontales = []
            pos_verticales = []

            for linea in lineas:
                x1, y1, x2, y2 = linea[0]
                dx = abs(x2 - x1)
                dy = abs(y2 - y1)
                longitud = math.hypot(dx, dy)
                if longitud < 35:
                    continue

                if dy < dx * 0.35:
                    pos_horizontales.append((y1 + y2) / 2.0)
                elif dx < dy * 0.35:
                    pos_verticales.append((x1 + x2) / 2.0)

            espaciados = []
            if len(pos_horizontales) >= 3:
                pos_h_ord = sorted(pos_horizontales)
                unicas_h = []
                for p in pos_h_ord:
                    if not unicas_h or abs(p - unicas_h[-1]) > 14:
                        unicas_h.append(p)
                for i in range(len(unicas_h) - 1):
                    diff = unicas_h[i+1] - unicas_h[i]
                    if 25 <= diff <= 65:
                        espaciados.append(diff)

            if len(pos_verticales) >= 3:
                pos_v_ord = sorted(pos_verticales)
                unicas_v = []
                for p in pos_v_ord:
                    if not unicas_v or abs(p - unicas_v[-1]) > 14:
                        unicas_v.append(p)
                for i in range(len(unicas_v) - 1):
                    diff = unicas_v[i+1] - unicas_v[i]
                    if 25 <= diff <= 65:
                        espaciados.append(diff)

            if espaciados:
                mediana_px = float(np.median(espaciados))
                if 28.0 <= mediana_px <= 55.0:
                    historial_calibracion_px.append(mediana_px)
                    PIXELES_POR_CM = float(np.median(historial_calibracion_px))
    except Exception:
        pass

    return PIXELES_POR_CM


# ==========================================
# ALGORITMOS DE DETECCIÓN: YOLOV8 + FALLBACK HSV
# ==========================================
def procesar_huevo_yolo(frame: np.ndarray) -> Tuple[bool, Optional[np.ndarray], Optional[np.ndarray], Optional[tuple]]:
    """
    Procesamiento mediante IA YOLOv8-seg.
    Ajusta la máscara morfológicamente para abarcar el huevo completo de punta a punta.
    """
    if modelo_yolo is None:
        return False, None, None, None
    try:
        resultados = modelo_yolo(frame, verbose=False, device=DEVICE_YOLO)
        if not resultados or resultados[0].masks is None or len(resultados[0].masks.data) == 0:
            return False, None, None, None

        masks_data = resultados[0].masks.data
        alto_frame, ancho_frame = frame.shape[:2]

        mejor_mascara = None
        max_area = 0
        for i in range(len(masks_data)):
            m = masks_data[i].cpu().numpy()
            m_resized = cv2.resize(m, (ancho_frame, alto_frame), interpolation=cv2.INTER_LINEAR)
            m_bin = (m_resized > 0.35).astype(np.uint8)
            
            # Cierre morfológico para rellenar extremos y asegurar continuidad de polo a polo
            kernel_close = cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (7, 7))
            m_bin = cv2.morphologyEx(m_bin, cv2.MORPH_CLOSE, kernel_close, iterations=2)
            
            area = cv2.countNonZero(m_bin)
            if area > max_area:
                max_area = area
                mejor_mascara = m_bin

        if mejor_mascara is None or max_area < 1500:
            return False, None, None, None

        contornos, _ = cv2.findContours(mejor_mascara, cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_NONE)
        if not contornos:
            return False, None, None, None

        cnt_principal = max(contornos, key=cv2.contourArea)
        if len(cnt_principal) < 5:
            return False, None, None, None

        # Convex Hull suave para garantizar contorno ovoide continuo
        cnt_hull = cv2.convexHull(cnt_principal)
        elipse = cv2.fitEllipse(cnt_hull if len(cnt_hull) >= 5 else cnt_principal)
        return True, mejor_mascara, cnt_principal, elipse
    except Exception as ex_yolo:
        tb_str = traceback.format_exc()
        log_debug(f"⚠️ Error en runtime de YOLOv8: {ex_yolo}. Se conmuta a fallback HSV.\nTraceback:\n{tb_str}")
        return False, None, None, None


def preprocesar_frame_luz(frame: np.ndarray) -> np.ndarray:
    """
    Preprocesamiento optimizado para mitigar parpadeo de tiras LED (rolling shutter)
    y reducir brillos especulares deslumbrantes (hotspots) en la cáscara del huevo.
    1. Filtro Bilateral: suavizado de alta frecuencia manteniendo bordes definidos.
    2. Atenuación de Hotspots especulares: atenúa zonas de brillo intenso (V > 235, S < 50 en HSV)
       para prevenir máscaras fraccionadas o bordes distorsionados en YOLO y HSV.
    """
    if frame is None:
        return frame
    try:
        # 1. Filtro Bilateral adaptativo para suavizar oscilaciones sin perder la silueta
        frame_filtrado = cv2.bilateralFilter(frame, d=7, sigmaColor=40, sigmaSpace=40)

        # 2. Atenuación localizada de reflejos especulares (puntos deslumbrantes)
        hsv = cv2.cvtColor(frame_filtrado, cv2.COLOR_BGR2HSV)
        mascara_hotspots = cv2.inRange(hsv, np.array([0, 0, 235]), np.array([180, 50, 255]))

        if cv2.countNonZero(mascara_hotspots) > 0:
            kernel_spot = cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (5, 5))
            mascara_hotspots = cv2.dilate(mascara_hotspots, kernel_spot, iterations=1)
            frame_suave = cv2.GaussianBlur(frame_filtrado, (15, 15), 0)
            mask_3c = cv2.cvtColor(mascara_hotspots, cv2.COLOR_GRAY2BGR)
            frame_filtrado = np.where(mask_3c > 0, cv2.addWeighted(frame_filtrado, 0.4, frame_suave, 0.6, 0), frame_filtrado)

        return frame_filtrado
    except Exception as ex_prep:
        print(f"⚠️ Error en preprocesar_frame_luz: {ex_prep}")
        return frame


def procesar_huevo_hsv(frame: np.ndarray) -> Tuple[bool, Optional[np.ndarray], Optional[np.ndarray], Optional[tuple]]:
    """
    Fallback funcional basado en umbralización HSV tradicional.
    Se activa automáticamente si YOLOv8 no está instalado, no cargó o falla en runtime.
    """
    try:
        hsv = cv2.cvtColor(cv2.bilateralFilter(cv2.GaussianBlur(frame, (5, 5), 0), 9, 75, 75), cv2.COLOR_BGR2HSV)
        mascara = cv2.bitwise_or(
            cv2.inRange(hsv, np.array([0, 50, 90]), np.array([30, 255, 255])),
            cv2.inRange(hsv, np.array([0, 0, 190]), np.array([180, 50, 255]))
        )
        kernel_open = cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (KERNEL_OPEN_SIZE, KERNEL_OPEN_SIZE))
        kernel_close = cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (KERNEL_CLOSE_SIZE, KERNEL_CLOSE_SIZE))
        mascara = cv2.morphologyEx(mascara, cv2.MORPH_OPEN, kernel_open, iterations=2)
        mascara = cv2.morphologyEx(mascara, cv2.MORPH_CLOSE, kernel_close, iterations=3)

        contornos, _ = cv2.findContours(mascara, cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_SIMPLE)
        if not contornos:
            return False, None, None, None

        cnt = max(contornos, key=cv2.contourArea)
        if cv2.contourArea(cnt) < 2000:
            return False, None, None, None

        # Rellenar fisuras o huecos internos provocados por reflejos sobre la superficie
        mascara_rellena = np.zeros_like(mascara)
        cv2.drawContours(mascara_rellena, [cnt], -1, 255, thickness=cv2.FILLED)

        elipse = cv2.fitEllipse(cnt)
        return True, mascara_rellena, cnt, elipse
    except Exception as ex_hsv:
        print(f"⚠️ Error en algoritmo de fallback HSV: {ex_hsv}")
        return False, None, None, None


def detectar_huevo(frame: np.ndarray) -> Tuple[bool, Optional[np.ndarray], Optional[np.ndarray], Optional[tuple], str]:
    """Intenta detección con YOLOv8-seg; si falla o no está disponible, cae suavemente al Fallback HSV."""
    frame_prep = preprocesar_frame_luz(frame)

    if modelo_yolo is not None:
        exito, mascara, contorno, elipse = procesar_huevo_yolo(frame_prep)
        if exito:
            return True, mascara, contorno, elipse, "YOLOv8-seg"

    exito, mascara, contorno, elipse = procesar_huevo_hsv(frame_prep)
    if exito:
        return True, mascara, contorno, elipse, "Fallback-HSV"

    return False, None, None, None, "Sin deteccion"


# ==========================================
# MANTENIMIENTO DE CONFIGURACIÓN Y HARDWARE
# ==========================================
def guardar_config_file(puerto, camara_idx, camara_nom, configurado=True):
    try:
        if not os.path.exists(CONFIG_DIR):
            os.makedirs(CONFIG_DIR, exist_ok=True)
        data = {
            "puerto_bascula": puerto,
            "camara_index": camara_idx,
            "camara_nombre": camara_nom,
            "configurado": configurado
        }
        with open(CONFIG_FILE, 'w', encoding='utf-8') as f:
            json.dump(data, f, indent=4, ensure_ascii=False)
        print(f"✅ Configuración guardada en {CONFIG_FILE}")
    except Exception as e:
        print(f"❌ Error guardando config.json: {e}")


def cargar_config_file():
    global PUERTO_BASCULA, CAMARA_INDEX, CAMARA_NOMBRE, CONFIGURADO
    if os.path.exists(CONFIG_FILE):
        try:
            with open(CONFIG_FILE, 'r', encoding='utf-8') as f:
                data = json.load(f)
                PUERTO_BASCULA = data.get('puerto_bascula', '')
                CAMARA_INDEX = int(data.get('camara_index', 0))
                CAMARA_NOMBRE = data.get('camara_nombre', f"Cámara {CAMARA_INDEX}")
                CONFIGURADO = data.get('configurado', False)
                print(f"ℹ️ Configuración cargada: Puerto='{PUERTO_BASCULA}', Cámara={CAMARA_INDEX}, Configurado={CONFIGURADO}")
        except Exception as e:
            print(f"⚠️ Error cargando config.json: {e}")


def abrir_hardware():
    global bascula, cap, PUERTO_BASCULA, CAMARA_INDEX, CONFIGURADO

    if bascula:
        try:
            if bascula.is_open:
                bascula.close()
        except Exception as ex:
            print(f"Error cerrando puerto serie previo: {ex}")
    bascula = None

    if PUERTO_BASCULA:
        try:
            bascula = serial.Serial(PUERTO_BASCULA, BAUDRATE, timeout=0.01)
            print(f"✅ Puerto {PUERTO_BASCULA} abierto con éxito.")
        except Exception as e:
            bascula = None
            print(f"⚠️ Error abriendo puerto serial {PUERTO_BASCULA}: {e}")

    if cap:
        try:
            if cap.isOpened():
                cap.release()
        except Exception as ex:
            print(f"Error liberando cámara previa: {ex}")
    cap = None

    if CONFIGURADO or PUERTO_BASCULA or CAMARA_NOMBRE:
        try:
            cap_temp = cv2.VideoCapture(CAMARA_INDEX, cv2.CAP_DSHOW)
            if not cap_temp.isOpened():
                cap_temp = cv2.VideoCapture(CAMARA_INDEX)

            if cap_temp.isOpened():
                cap_temp.set(cv2.CAP_PROP_FRAME_WIDTH, 1280)
                cap_temp.set(cv2.CAP_PROP_FRAME_HEIGHT, 720)
                try:
                    cap_temp.set(cv2.CAP_PROP_FPS, CAMARA_FPS)
                except Exception:
                    pass

                # Desactivar auto-enfoque si la cámara lo soporta
                try:
                    cap_temp.set(cv2.CAP_PROP_AUTOFOCUS, 1.0 if CAMARA_AUTOFOCUS else 0.0)
                except Exception:
                    pass

                # Desactivar auto-balance de blancos si se especifica
                try:
                    cap_temp.set(cv2.CAP_PROP_AUTO_WB, 1.0 if CAMARA_AUTO_WB else 0.0)
                except Exception:
                    pass

                if CAMARA_EXPOSURE_MANUAL:
                    try:
                        cap_temp.set(cv2.CAP_PROP_AUTO_EXPOSURE, 0.25)  # modo manual en DirectShow
                        cap_temp.set(cv2.CAP_PROP_EXPOSURE, CAMARA_EXPOSURE_VALOR)
                        print(f"✅ Exposición manual fijada en {CAMARA_EXPOSURE_VALOR}")
                    except Exception as ex_exp:
                        print(f"⚠️ No se pudo fijar exposición manual: {ex_exp}")

                if CAMARA_GAIN_MANUAL:
                    try:
                        cap_temp.set(cv2.CAP_PROP_GAIN, CAMARA_GAIN_VALOR)
                        print(f"✅ Ganancia fijada en {CAMARA_GAIN_VALOR}")
                    except Exception as ex_gain:
                        print(f"⚠️ No se pudo fijar ganancia manual: {ex_gain}")

                cap = cap_temp
                print(f"✅ Cámara {CAMARA_INDEX} abierta con éxito y parámetros anti-flicker aplicados.")
            else:
                print(f"⚠️ No se pudo abrir la cámara {CAMARA_INDEX}.")
        except Exception as e:
            cap = None
            print(f"⚠️ Error abriendo cámara {CAMARA_INDEX}: {e}")


def leer_peso_bascula():
    global peso_actual_memoria, ultimo_timestamp_serial, ciclos_sin_datos
    if bascula and bascula.is_open:
        try:
            if bascula.in_waiting > 0:
                ciclos_sin_datos = 0
                data_bytes = bascula.read(bascula.in_waiting)
                linea = data_bytes.decode('utf-8', errors='ignore').strip()
                print(f"RAW Báscula: {linea}")
                if linea:
                    nums = re.findall(r'\d+\.\d+|\d+', linea)
                    if nums:
                        p = float(nums[-1])
                        if p < 5.0: p *= 1000
                        if p < 2.0:
                            historial_peso.clear()
                            historial_volumen.clear()
                            peso_actual_memoria = 0.0
                        else:
                            historial_peso.append(p)
                            peso_actual_memoria = sum(historial_peso) / len(historial_peso)
                        ultimo_timestamp_serial = time.time()
            else:
                ciclos_sin_datos += 1
                if ciclos_sin_datos >= 2:
                    print("Sin datos en el buffer")
        except Exception as e:
            print(f"⚠️ Error leyendo báscula: {e}")

    if time.time() - ultimo_timestamp_serial > 1.2:
        historial_peso.clear()
        historial_volumen.clear()
        peso_actual_memoria = 0.0

    return peso_actual_memoria


# ==========================================
# HILO DE INFERENCIA ASÍNCRONA DE IA
# ==========================================
def loop_inferencia():
    """
    Hilo dedicado a ejecutar la inferencia de YOLOv8 de forma asíncrona.
    No bloquea la captura de cámara, lectura serial de la báscula ni el streaming HTTP.
    """
    global ultima_deteccion, vision_corriendo, pausa_vision
    while vision_corriendo:
        try:
            if pausa_vision:
                time.sleep(0.1)
                continue

            frame_local = None
            with lock_inferencia:
                if frame_para_inferencia is not None:
                    frame_local = frame_para_inferencia.copy()

            if frame_local is not None:
                resultado = detectar_huevo(frame_local)
                with lock_inferencia:
                    ultima_deteccion = resultado

            time.sleep(INTERVALO_INFERENCIA_SEG)
        except Exception as ex_inf_loop:
            print(f"⚠️ Error en loop_inferencia: {ex_inf_loop}")
            time.sleep(0.2)


# ==========================================
# LOOP PRINCIPAL DE VISIÓN (ALTA VELOCIDAD & STREAMING)
# ==========================================
def loop_vision():
    """
    Hilo principal de captura de video y báscula. Se encarga de refrescar a máxima velocidad
    el streaming HTTP /frame.jpg y leer el puerto COM serial sin esperar el tiempo de inferencia de YOLO.
    """
    global datos_globales, frame_actual, cap, bascula, pausa_vision, vision_corriendo, frame_para_inferencia

    with lock_hardware:
        abrir_hardware()

    while vision_corriendo:
        try:
            if pausa_vision:
                time.sleep(0.1)
                continue

            frame = None
            peso_vivo = 0.0

            # 1. Acceso a Hardware (VideoCapture + Serial COM) protegido exclusivamente por lock_hardware
            with lock_hardware:
                # Leer SIEMPRE la báscula en cada iteración del loop de hardware
                peso_vivo = leer_peso_bascula()

                if cap is None or not cap.isOpened():
                    datos_globales["peso"] = round(peso_vivo, 1)
                    time.sleep(0.1)
                    continue

                ret, frame = cap.read()
                if not ret or frame is None:
                    datos_globales["peso"] = round(peso_vivo, 1)
                    time.sleep(0.05)
                    continue

            # Actualizar SIEMPRE el peso en datos_globales de forma inmediata tras leer la báscula
            datos_globales["peso"] = round(peso_vivo, 1)

            # 2. Actualizar el frame más reciente para que el hilo de inferencia lo procese
            with lock_inferencia:
                frame_para_inferencia = frame.copy()
                det_actual = ultima_deteccion

            # 3. Leer la última inferencia disponible (sin esperar a YOLO) y renderizar overlays
            objeto_valido, mascara_px, contorno, elipse, metodo_usado = det_actual
            datos_globales["metodo_deteccion"] = metodo_usado if metodo_usado not in ("Inicializando", "Sin deteccion") else ("YOLOv8-seg" if modelo_yolo is not None else "Fallback-HSV")

            # Calibración dinámica periódica de escala (1cm x 1cm) cada 1.5 segundos
            global ultimo_check_calibracion
            ahora = time.time()
            if ahora - ultimo_check_calibracion > 1.5:
                ultimo_check_calibracion = ahora
                calibrar_pixeles_desde_cuadricula(frame, mascara_px if objeto_valido else None)

            if objeto_valido and elipse is not None:
                (x, y), (d1, d2), angulo = elipse
                eje_mayor = max(d1, d2)
                eje_menor = min(d1, d2)

                if eje_menor > 0 and 1.05 <= (eje_mayor / eje_menor) <= 2.2:
                    largo = eje_mayor / PIXELES_POR_CM
                    ancho = eje_menor / PIXELES_POR_CM

                    historial_largo.append(largo)
                    historial_ancho.append(ancho)

                    l_cm = sum(historial_largo) / len(historial_largo)
                    a_cm = sum(historial_ancho) / len(historial_ancho)

                    v_elip = volumen_elipsoide(l_cm, a_cm)
                    v_pappus = volumen_por_contorno(mascara_px, PIXELES_POR_CM)
                    v_narushin = volumen_narushin(l_cm, a_cm)

                    if v_pappus is not None and (0.5 * v_elip <= v_pappus <= 1.5 * v_elip):
                        v_rev = v_pappus
                    else:
                        v_rev = v_narushin if v_narushin > 0 else (v_elip * 0.98)

                    v_geom = (v_elip + v_rev) / 2.0

                    # =========================================================
                    # VALIDACIÓN DE COHERENCIA FÍSICA (Anti-Puño / Anti-Objetos)
                    # =========================================================
                    peso_esperado = v_geom * DENSIDAD_HUEVO
                    es_coherente = True
                    motivo_invalido = ""

                    # 1. Filtro dimensional: Admite huevos AAA/Jumbo y compensa distorsión de lente
                    if l_cm > 10.5 or a_cm > 7.5 or v_geom > 120.0:
                        es_coherente = False
                        motivo_invalido = "Tamaño excede huevo real"

                    # 2. Filtro de coherencia masa-volumen con la báscula (intacto)
                    if peso_vivo >= 5.0:
                        discrepancia = abs(peso_vivo - peso_esperado) / max(peso_esperado, 1.0)
                        # Margen de tolerancia del 38% entre peso real y esperado por volumen
                        if discrepancia > 0.38:
                            es_coherente = False
                            motivo_invalido = f"Discrepancia masa-volumen ({int(discrepancia*100)}%)"
                    elif v_geom > 40.0 and peso_vivo > 1.0 and peso_vivo < 5.0:
                        # Objeto voluminoso (> 40 cm³) con masa insignificante (ej. puño flotando rozando la báscula)
                        es_coherente = False
                        motivo_invalido = "Masa insuficiente para volumen detectado"

                    if peso_vivo >= 5.0:
                        v_den = peso_vivo / DENSIDAD_HUEVO
                        # Ponderación armónica: 75% volumen físico por densidad (Arquímedes) + 25% ajuste geométrico óptico
                        v_real = 0.75 * v_den + 0.25 * min(max(v_geom, 0.85 * v_den), 1.15 * v_den)
                    else:
                        v_den = 0.0
                        v_real = v_geom

                    if v_real > 120.0:
                        v_real = min(v_elip, 120.0)

                    historial_volumen.append(v_real)
                    v_estabilizado = float(np.median(historial_volumen))

                    if not es_coherente:
                        categoria_actual = "OBJETO INVÁLIDO"
                        metodo_usado = f"Incoherente ({motivo_invalido})"
                        huevo_valido = False
                        color_render = (0, 0, 255)  # Rojo alerta
                    else:
                        categoria_actual = clasificar_huevo(peso_vivo) if peso_vivo > 0 else "ESPERANDO..."
                        huevo_valido = True
                        color_render = (0, 255, 255)  # Amarillo normal

                    datos_globales.update({
                        "largo": round(l_cm, 2),
                        "ancho": round(a_cm, 2),
                        "peso": round(peso_vivo, 1),
                        "elipsoide": round(v_elip, 1),
                        "revolucion": round(v_rev, 1),
                        "bascula": round(v_den, 1),
                        "volumen_real": round(v_estabilizado, 1),
                        "categoria": categoria_actual,
                        "metodo_deteccion": metodo_usado,
                        "huevo_detectado": huevo_valido,
                        "es_valido": huevo_valido
                    })

                    # Trazar elipse y contorno exactos
                    cv2.ellipse(frame, elipse, color_render, 2)
                    if contorno is not None:
                        cv2.drawContours(frame, [contorno], -1, color_render, 1)

                    texto = f"{categoria_actual} - {peso_vivo:.1f}g [{metodo_usado}]" if huevo_valido and peso_vivo > 0 else f"{categoria_actual} [{metodo_usado}]"
                    cv2.putText(frame, texto, (int(x) - 80, int(y) - int(eje_mayor / 2) - 10),
                                cv2.FONT_HERSHEY_SIMPLEX, 0.55, color_render, 2)
                else:
                    objeto_valido = False
            elif peso_vivo >= 5.0:
                v_den = peso_vivo / DENSIDAD_HUEVO
                historial_volumen.append(v_den)
                v_estabilizado = float(np.median(historial_volumen))
                datos_globales.update({
                    "peso": round(peso_vivo, 1),
                    "bascula": round(v_den, 1),
                    "volumen_real": round(v_estabilizado, 1),
                    "categoria": "OBJETO INVÁLIDO",
                    "metodo_deteccion": "Objeto no reconocido (Visión)",
                    "huevo_detectado": False,
                    "es_valido": False
                })

            if not objeto_valido and peso_vivo < 5.0:
                historial_largo.clear()
                historial_ancho.clear()
                historial_volumen.clear()
                datos_globales.update({
                    "huevo_detectado": False,
                    "es_valido": False
                })

            if peso_vivo <= 2.0:
                historial_largo.clear()
                historial_ancho.clear()
                historial_volumen.clear()
                datos_globales.update({
                    "largo": 0.0,
                    "ancho": 0.0,
                    "elipsoide": 0.0,
                    "revolucion": 0.0,
                    "bascula": 0.0,
                    "volumen_real": 0.0,
                    "categoria": "ESPERANDO...",
                    "huevo_detectado": False,
                    "es_valido": False
                })

            # 4. Actualizar buffer de streaming HTTP sin contención con la IA
            with lock_frame:
                ok, buffer = cv2.imencode('.jpg', frame, [int(cv2.IMWRITE_JPEG_QUALITY), 70])
                if ok:
                    frame_actual = buffer.tobytes()

            time.sleep(0.01)
        except Exception as e:
            print(f"⚠️ Error inesperado en loop_vision: {e}")
            traceback.print_exc()
            time.sleep(0.2)


# ==========================================
# ENDPOINTS REST HTTP (CONTRATO WPF INTACTO)
# ==========================================
@app.route('/datos-huevo', methods=['GET'])
def get_datos():
    return jsonify(datos_globales)


@app.route('/frame.jpg', methods=['GET'])
def get_frame():
    with lock_frame:
        if frame_actual is None:
            return '', 204
        return Response(frame_actual, mimetype='image/jpeg')


@app.route('/dispositivos-disponibles', methods=['GET'])
def get_dispositivos():
    basculas_list = []
    try:
        ports = serial.tools.list_ports.comports()
        for p in ports:
            desc = f"{p.device}"
            if p.description and p.description != "n/a":
                desc += f" - {p.description}"
            basculas_list.append({
                "id": str(p.device),
                "nombre": desc,
                "puerto": str(p.device),
                "descripcion": desc
            })
    except Exception as e:
        print(f"Error listando puertos COM: {e}")

    camaras_list = []
    global pausa_vision
    pausa_vision = True
    time.sleep(0.05)

    try:
        acquired = lock_hardware.acquire(timeout=1.0)
        if acquired:
            try:
                try:
                    import comtypes
                    comtypes.CoInitialize()
                except Exception:
                    pass
                from pygrabber.dshow_graph import FilterGraph
                graph = FilterGraph()
                devices = graph.get_input_devices()
                for idx, dev_name in enumerate(devices):
                    es_principal = (idx == CAMARA_INDEX and cap is not None and cap.isOpened())
                    nombre_label = f"{dev_name} (Principal)" if es_principal else dev_name
                    camaras_list.append({
                        "id": idx,
                        "nombre": nombre_label
                    })
            except Exception as e_pyg:
                print(f"⚠️ Error enumerando cámaras con pygrabber: {e_pyg}")
            finally:
                lock_hardware.release()
        else:
            print("⚠️ Timeout al intentar bloquear hardware para escaneo de cámaras.")
    finally:
        pausa_vision = False

    # Fallback sin OpenCV ni locks de hardware usando PowerShell nativo
    if not camaras_list:
        try:
            log_debug("ℹ️ Ejecutando fallback de cámaras con PowerShell (Get-PnpDevice)...")
            cmd = [
                "powershell",
                "-NoProfile",
                "-Command",
                "Get-PnpDevice -PresentOnly -Class Camera,Image | Select-Object -ExpandProperty FriendlyName"
            ]
            resultado = subprocess.run(
                cmd,
                capture_output=True,
                text=True,
                timeout=5,
                encoding='utf-8',
                errors='ignore'
            )
            if resultado.returncode == 0 and resultado.stdout:
                lineas = [linea.strip() for linea in resultado.stdout.splitlines() if linea.strip()]
                for idx, dev_name in enumerate(lineas):
                    es_principal = (idx == CAMARA_INDEX and cap is not None and cap.isOpened())
                    nombre_label = f"{dev_name} (Principal)" if es_principal else dev_name
                    camaras_list.append({
                        "id": idx,
                        "nombre": nombre_label
                    })
        except Exception as e_ps:
            print(f"⚠️ Error en fallback de cámaras con PowerShell: {e_ps}")

    # Garantizar al menos la cámara principal si está activa y no se detectaron cámaras en PowerShell
    if not camaras_list and cap is not None and cap.isOpened():
        camaras_list.append({
            "id": CAMARA_INDEX,
            "nombre": f"Cámara {CAMARA_INDEX} (Principal)"
        })

    print(f"[DEBUG DISPOSITIVOS] Camaras: {camaras_list}, Basculas: {basculas_list}")
    return jsonify({
        "camaras": camaras_list,
        "basculas": basculas_list,
        "puertos": basculas_list
    })


@app.route('/estado-configuracion', methods=['GET'])
def get_estado():
    esta_conectado = CONFIGURADO and (
        (cap is not None and cap.isOpened()) or
        (bascula is not None and bascula.is_open)
    )
    return jsonify({
        "puerto_bascula": PUERTO_BASCULA,
        "camara_index": CAMARA_INDEX,
        "camara_nombre": CAMARA_NOMBRE,
        "configurado": CONFIGURADO,
        "conectado": esta_conectado
    })


@app.route('/guardar-configuracion', methods=['POST'])
def guardar_config():
    global PUERTO_BASCULA, CAMARA_INDEX, CAMARA_NOMBRE, CONFIGURADO, pausa_vision
    data = request.get_json() or {}

    puerto = data.get('puerto_bascula', '')
    camara_idx_raw = data.get('camara_index')
    if camara_idx_raw is None:
        camara_idx_raw = data.get('camara_nombre')

    if camara_idx_raw is not None:
        try:
            camara_idx = int(camara_idx_raw)
        except:
            nums = re.findall(r'\d+', str(camara_idx_raw))
            camara_idx = int(nums[0]) if nums else 0
    else:
        camara_idx = 0

    camara_nom = f"Cámara {camara_idx}"

    pausa_vision = True
    time.sleep(0.15)

    with lock_hardware:
        PUERTO_BASCULA = puerto
        CAMARA_INDEX = camara_idx
        CAMARA_NOMBRE = camara_nom
        CONFIGURADO = True

        abrir_hardware()
        guardar_config_file(PUERTO_BASCULA, CAMARA_INDEX, CAMARA_NOMBRE, True)

    pausa_vision = False

    esta_conectado = (
        (cap is not None and cap.isOpened()) or
        (bascula is not None and bascula.is_open)
    )

    return jsonify({
        "status": "ok",
        "message": "Configuración guardada y conectada de forma segura",
        "puerto_bascula": PUERTO_BASCULA,
        "camara_index": CAMARA_INDEX,
        "camara_nombre": CAMARA_NOMBRE,
        "conectado": esta_conectado
    })


@app.route('/desconectar-hardware', methods=['POST'])
def desconectar_hardware():
    global cap, bascula, CONFIGURADO, pausa_vision, frame_actual
    pausa_vision = True
    time.sleep(0.15)

    with lock_hardware:
        if cap:
            try:
                if cap.isOpened():
                    cap.release()
            except Exception as ex:
                print(f"Error liberando cámara al desconectar: {ex}")
            cap = None

        if bascula:
            try:
                if bascula.is_open:
                    bascula.close()
            except Exception as ex:
                print(f"Error cerrando báscula al desconectar: {ex}")
            bascula = None

        with lock_frame:
            frame_actual = None

        CONFIGURADO = False

    pausa_vision = False

    return jsonify({
        "status": "ok",
        "message": "Hardware desconectado correctamente",
        "conectado": False
    })


@app.route('/shutdown', methods=['POST'])
def shutdown_server():
    global vision_corriendo, pausa_vision, cap, bascula
    vision_corriendo = False
    pausa_vision = True

    def _limpiar_y_salir():
        try:
            acquired = lock_hardware.acquire(timeout=1.0)
            if cap and cap.isOpened():
                try: cap.release()
                except: pass
            if bascula and bascula.is_open:
                try: bascula.close()
                except: pass
            if acquired:
                lock_hardware.release()
        except Exception as e:
            print(f"Aviso limpiando hardware en shutdown: {e}")
        time.sleep(0.2)
        os._exit(0)

    threading.Thread(target=_limpiar_y_salir, daemon=True).start()

    return jsonify({"status": "ok", "message": "Servidor apaga la sesión limpiamente"})


if __name__ == '__main__':
    cargar_config_file()

    # Iniciar hilo de visión (captura rápida de cámara + báscula)
    t_vision = threading.Thread(target=loop_vision, daemon=True)
    t_vision.start()

    # Iniciar hilo asíncrono de inferencia YOLOv8
    t_inferencia = threading.Thread(target=loop_inferencia, daemon=True)
    t_inferencia.start()

    app.run(host='0.0.0.0', port=5001, threaded=True)