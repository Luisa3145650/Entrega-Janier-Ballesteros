from flask import Flask, jsonify, Response, request
from flask_cors import CORS
import cv2
import numpy as np
import serial
import serial.tools.list_ports
import re
import os
import sys
import json
import signal
import atexit
from collections import deque
import threading
import time

try:
    from pygrabber.dshow_graph import FilterGraph
    PYGRABBER_DISPONIBLE = True
except ImportError:
    PYGRABBER_DISPONIBLE = False

app = Flask(__name__)
CORS(app)

# ==========================================
# CONFIGURACIÓN DEL SISTEMA (calibración - NO depende del PC)
# ==========================================
# IMPORTANTE: recalibra este valor usando calibrar_camara.py con un objeto de
# medida conocida puesto exactamente donde va el huevo, a esta misma resolución
# (640x480). Calibrado el 14/08/2026 con la HD Pro Webcam C920 a 8cm de distancia,
# usando un huevo real de 6cm (largo) x 4cm (ancho) como referencia:
#   478.2px / 6cm = 79.70   (usando el eje mayor)
#   308.1px / 4cm = 77.03   (usando el eje menor)
#   promedio = 78.36
# Si cambias la distancia camara-huevo o la camara, vuelve a calibrar.
PIXELES_POR_CM = 78.36
DENSIDAD_HUEVO = 1.07
FACTOR_FORMA = 0.517
BAUDRATE = 9600

# ==========================================
# REGIÓN DE INTERÉS (ROI) - opcional
# ==========================================
# Si en el video ves partes de la pared, ropa, o cualquier objeto ajeno al huevo,
# actívalo aquí y ajusta los 4 valores para que el rectángulo cubra SOLO la zona
# de la bandeja/plato de la báscula donde se pone el huevo. Con la cámara tan
# cerca del huevo como en tu montaje, puede que no lo necesites, pero déjalo listo.
USAR_ROI = False
ROI_X1, ROI_Y1, ROI_X2, ROI_Y2 = 100, 60, 540, 420  # ajusta viendo tu frame de 640x480

# ==========================================
# FILTROS DE FORMA (para no confundir huevo con ruido/fondo)
# ==========================================
AREA_MINIMA = 2000
AREA_MAXIMA = 60000          # descarta manchas gigantes (paredes, sombras extendidas)
CIRCULARIDAD_MINIMA = 0.75   # 1.0 = círculo perfecto; un huevo real ronda 0.85-0.95
SOLIDEZ_MINIMA = 0.92        # contorno "limpio" sin muescas ni ruido irregular
RELACION_EJES_MIN = 1.15
RELACION_EJES_MAX = 1.7

# Cuántos frames seguidos válidos se piden antes de aceptar la medida como estable
FRAMES_CONSECUTIVOS_REQUERIDOS = 5

# ==========================================
# CONFIGURACIÓN POR MÁQUINA (puerto/cámara) - SÍ depende del PC
# Se guarda fuera del proyecto para que sobreviva reinstalaciones
# y para que cada computador tenga la suya sin tocar el código.
# ==========================================
RUTA_CONFIG = os.path.join(
    os.environ.get("PROGRAMDATA", r"C:\ProgramData"),
    "ClasificadorHuevos",
    "config.json"
)

CONFIG_POR_DEFECTO = {
    "puerto_bascula": None,     # ej: "COM3". None = sin configurar todavia
    "camara_nombre": None,      # ej: "HD Pro Webcam C920". None = usa indice 0 de respaldo
    "configurado": False
}


def cargar_config():
    if os.path.exists(RUTA_CONFIG):
        try:
            with open(RUTA_CONFIG, "r", encoding="utf-8") as f:
                datos = json.load(f)
                # aseguramos que existan todas las claves aunque el archivo sea viejo
                config = dict(CONFIG_POR_DEFECTO)
                config.update(datos)
                return config
        except Exception as e:
            print(f"No se pudo leer config.json, se usaran valores por defecto: {e}")
    return dict(CONFIG_POR_DEFECTO)


def guardar_config(config):
    os.makedirs(os.path.dirname(RUTA_CONFIG), exist_ok=True)
    with open(RUTA_CONFIG, "w", encoding="utf-8") as f:
        json.dump(config, f, indent=2, ensure_ascii=False)
    print(f"Configuracion guardada en {RUTA_CONFIG}: {config}")


config_actual = cargar_config()

datos_globales = {
    "largo": 0.0,
    "ancho": 0.0,
    "peso": 0.0,
    "elipsoide": 0.0,
    "revolucion": 0.0,
    "bascula": 0.0,
    "volumen_real": 0.0,
    "categoria": "ESPERANDO..."
}

# --- Buffer del último frame procesado, protegido por lock ---
frame_actual = None
lock_frame = threading.Lock()

# --- Referencias globales para poder liberarlas desde cualquier lado (shutdown, señales, etc.) ---
cap_global = None
bascula = None

# --- Flags para reconfiguracion en caliente (se activan desde /guardar-configuracion) ---
solicitud_reinicio_camara = False
solicitud_reinicio_bascula = False

# --- Contador de frames consecutivos con deteccion valida (para estabilizar la lectura) ---
frames_validos_consecutivos = 0


# ==========================================
# DETECCIÓN Y LISTADO DE DISPOSITIVOS (para que el WPF pueda ofrecerlos al usuario)
# ==========================================

def listar_puertos_disponibles():
    """Devuelve todos los puertos COM que Windows detecta ahora mismo, con su descripcion,
    para que el usuario elija cual es la bascula en cada PC."""
    try:
        return [{"puerto": p.device, "descripcion": p.description} for p in serial.tools.list_ports.comports()]
    except Exception as e:
        print(f"Error listando puertos: {e}")
        return []


def listar_camaras_disponibles():
    """Devuelve los nombres de las camaras conectadas, para que el usuario elija cual usar."""
    if not PYGRABBER_DISPONIBLE:
        return []
    try:
        import comtypes
        comtypes.CoInitialize()
    except Exception:
        pass
    try:
        graph = FilterGraph()
        return graph.get_input_devices()
    except Exception as e:
        print(f"Error listando camaras: {e}")
        return []
    finally:
        try:
            import comtypes
            comtypes.CoUninitialize()
        except Exception:
            pass


def encontrar_indice_camara(nombre_buscado):
    """Busca el indice de OpenCV que corresponde a una camara por su nombre exacto.
    Devuelve None si no la encuentra o si pygrabber no esta instalado."""
    if not nombre_buscado or not PYGRABBER_DISPONIBLE:
        return None

    try:
        import comtypes
        comtypes.CoInitialize()
    except Exception:
        pass

    try:
        graph = FilterGraph()
        dispositivos = graph.get_input_devices()
        indice_encontrado = None
        for indice, nombre in enumerate(dispositivos):
            if nombre_buscado.lower() in nombre.lower():
                print(f"Camara '{nombre}' encontrada en el indice {indice}.")
                indice_encontrado = indice
                break
        if indice_encontrado is None:
            print(f"No se encontro ninguna camara que contenga '{nombre_buscado}'. Dispositivos disponibles: {dispositivos}")
        return indice_encontrado
    except Exception as e:
        print(f"Error buscando camaras con pygrabber: {e}")
        return None
    finally:
        try:
            import comtypes
            comtypes.CoUninitialize()
        except Exception:
            pass


# ==========================================
# LIBERACIÓN DE RECURSOS - GARANTIZADA PASE LO QUE PASE
# ==========================================

def liberar_recursos():
    """Se ejecuta SIEMPRE que el proceso termina (normal, Ctrl+C, kill, o vía /shutdown).
    Sin esto, Windows deja la camara y el puerto COM reservados aunque el proceso ya no exista."""
    global cap_global, bascula
    print("Liberando recursos (camara y bascula)...")
    try:
        if cap_global is not None:
            cap_global.release()
            print("Camara liberada.")
    except Exception as e:
        print(f"Error liberando camara: {e}")
    try:
        if bascula and bascula.is_open:
            bascula.close()
            print("Puerto de bascula liberado.")
    except Exception as e:
        print(f"Error liberando bascula: {e}")


def _manejar_señal(signum, frame):
    liberar_recursos()
    sys.exit(0)


atexit.register(liberar_recursos)
signal.signal(signal.SIGINT, _manejar_señal)   # Ctrl+C
signal.signal(signal.SIGTERM, _manejar_señal)  # kill / cierre del proceso


# ==========================================
# BÁSCULA
# ==========================================

def abrir_bascula_con_reintentos(puerto=None, intentos=5, espera_segundos=2):
    """Intenta abrir el puerto serial varias veces antes de rendirse.
    Si 'puerto' es None (sin configurar todavia), no intenta nada."""
    puerto = puerto or config_actual.get("puerto_bascula")
    if not puerto:
        print("Ningun puerto de bascula configurado todavia. Usa /guardar-configuracion para asignar uno.")
        return None

    for intento in range(1, intentos + 1):
        try:
            conexion = serial.Serial(puerto, BAUDRATE, timeout=0.01)
            print(f"Puerto {puerto} abierto con exito (intento {intento}/{intentos}).")
            return conexion
        except Exception as e:
            print(f"Intento {intento}/{intentos} fallido abriendo {puerto}: {e}")
            if intento < intentos:
                time.sleep(espera_segundos)
    print(f"No se pudo abrir {puerto} tras {intentos} intentos. La bascula quedara inactiva (peso siempre 0).")
    return None


bascula = abrir_bascula_con_reintentos()

historial_largo = deque(maxlen=5)
historial_ancho = deque(maxlen=5)
historial_peso = deque(maxlen=2)

peso_actual_memoria = 0.0
ultimo_timestamp_serial = time.time()


def clasificar_huevo(peso_g):
    if peso_g < 45.0: return "TIPO C"
    elif 45.0 <= peso_g < 53.0: return "TIPO B"
    elif 53.0 <= peso_g < 60.0: return "TIPO A"
    elif 60.0 <= peso_g < 67.0: return "TIPO AA"
    elif 67.0 <= peso_g < 78.0: return "TIPO AAA"
    else: return "JUMBO"


def leer_peso_bascula():
    global peso_actual_memoria, ultimo_timestamp_serial, bascula, solicitud_reinicio_bascula

    # Reconfiguracion en caliente: si desde /guardar-configuracion se pidio cambiar de puerto
    if solicitud_reinicio_bascula:
        print("Reconectando bascula con la nueva configuracion...")
        try:
            if bascula and bascula.is_open:
                bascula.close()
        except Exception:
            pass
        bascula = abrir_bascula_con_reintentos(puerto=config_actual.get("puerto_bascula"), intentos=1, espera_segundos=0)
        historial_peso.clear()
        peso_actual_memoria = 0.0
        ultimo_timestamp_serial = time.time()
        solicitud_reinicio_bascula = False

    if bascula and bascula.is_open:
        try:
            if bascula.in_waiting > 0:
                data_bytes = bascula.read(bascula.in_waiting)
                linea = data_bytes.decode('utf-8', errors='ignore').strip()
                if linea:
                    nums = re.findall(r'\d+\.\d+|\d+', linea)
                    if nums:
                        p = float(nums[-1])
                        if p < 5.0: p *= 1000
                        if p < 2.0:
                            historial_peso.clear()
                            peso_actual_memoria = 0.0
                        else:
                            historial_peso.append(p)
                            peso_actual_memoria = sum(historial_peso) / len(historial_peso)
                        ultimo_timestamp_serial = time.time()
        except Exception:
            pass

    # Watchdog: si la bascula no emite señales en 2 segundos, forzamos cero absoluto.
    if time.time() - ultimo_timestamp_serial > 2.0:
        historial_peso.clear()
        peso_actual_memoria = 0.0

    return peso_actual_memoria


# ==========================================
# CÁMARA
# ==========================================

def abrir_camara(nombre_camara):
    # El emparejamiento por NOMBRE via pygrabber puede fallar (el orden que reporta
    # pygrabber no siempre coincide con el indice real de OpenCV/DirectShow). Si ya
    # calibraste con calibrar_camara.py y confirmaste visualmente el indice correcto,
    # ese indice quedo guardado en indice_camara.json junto al script: lo usamos aqui
    # directamente para evitar que el servidor abra la camara equivocada.
    ruta_indice_guardado = os.path.join(os.path.dirname(os.path.abspath(__file__)), "indice_camara.json")
    indice_camara = None

    if os.path.exists(ruta_indice_guardado):
        try:
            with open(ruta_indice_guardado, "r") as f:
                indice_camara = json.load(f).get("indice")
                print(f"Usando indice de camara confirmado manualmente: {indice_camara}")
        except Exception:
            indice_camara = None

    if indice_camara is None:
        indice_camara = encontrar_indice_camara(nombre_camara)
        if indice_camara is None:
            indice_camara = 0
            print(f"ADVERTENCIA: usando indice de camara de respaldo ({indice_camara}). Puede no ser la camara correcta.")
            print("Recomendado: corre calibrar_camara.py para confirmar visualmente el indice correcto.")

    cap_nueva = cv2.VideoCapture(indice_camara, cv2.CAP_DSHOW)
    cap_nueva.set(cv2.CAP_PROP_FOURCC, cv2.VideoWriter_fourcc(*'MJPG'))  # captura mas rapida que el formato por defecto
    cap_nueva.set(cv2.CAP_PROP_FRAME_WIDTH, 640)
    cap_nueva.set(cv2.CAP_PROP_FRAME_HEIGHT, 480)
    cap_nueva.set(cv2.CAP_PROP_FPS, 30)
    cap_nueva.set(cv2.CAP_PROP_BUFFERSIZE, 1)  # evita acumular frames viejos (efecto "camara lenta")

    if not cap_nueva.isOpened():
        print(f"ERROR CRITICO: no se pudo abrir la camara en el indice {indice_camara}.")

    return cap_nueva


def loop_vision():
    global datos_globales, frame_actual, cap_global, solicitud_reinicio_camara, frames_validos_consecutivos

    cap = abrir_camara(config_actual.get("camara_nombre"))
    cap_global = cap

    try:
        while True:
            # Todo el cuerpo del loop va protegido para que un error puntual (una excepcion
            # no prevista en OpenCV, en el parseo de la bascula, en la codificacion JPEG, etc.)
            # no mate el hilo completo.
            try:
                # Reconfiguracion en caliente: si desde /guardar-configuracion se pidio cambiar de camara
                if solicitud_reinicio_camara:
                    print("Reconectando camara con la nueva configuracion...")
                    try:
                        cap.release()
                    except Exception:
                        pass
                    cap = abrir_camara(config_actual.get("camara_nombre"))
                    cap_global = cap
                    solicitud_reinicio_camara = False

                ret, frame = cap.read()
                if not ret:
                    print("La camara dejo de entregar frames. Reintentando en 1s (sin matar el hilo)...")
                    time.sleep(1)
                    try:
                        cap.release()
                    except Exception:
                        pass
                    cap = abrir_camara(config_actual.get("camara_nombre"))
                    cap_global = cap
                    continue

                peso_vivo = leer_peso_bascula()

                # --- Recorte a la region de interes (si esta activado) ---
                if USAR_ROI:
                    frame_analisis = frame[ROI_Y1:ROI_Y2, ROI_X1:ROI_X2]
                    offset_x, offset_y = ROI_X1, ROI_Y1
                else:
                    frame_analisis = frame
                    offset_x, offset_y = 0, 0

                hsv = cv2.cvtColor(cv2.GaussianBlur(frame_analisis, (5, 5), 0), cv2.COLOR_BGR2HSV)
                mascara = cv2.bitwise_or(cv2.inRange(hsv, np.array([0, 50, 90]), np.array([30, 255, 255])),
                                            cv2.inRange(hsv, np.array([0, 0, 200]), np.array([180, 30, 255])))
                kernel = cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (9, 9))
                mascara = cv2.morphologyEx(cv2.morphologyEx(mascara, cv2.MORPH_OPEN, kernel, iterations=2), cv2.MORPH_CLOSE, kernel, iterations=3)

                contornos, _ = cv2.findContours(mascara, cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_SIMPLE)

                objeto_valido = False
                if contornos:
                    cnt = max(contornos, key=cv2.contourArea)
                    area = cv2.contourArea(cnt)

                    if AREA_MINIMA < area < AREA_MAXIMA:
                        # --- Filtros de forma: descartan manchas de fondo (pared, ropa, sombras) ---
                        perimetro = cv2.arcLength(cnt, True)
                        circularidad = (4 * np.pi * area / (perimetro ** 2)) if perimetro > 0 else 0

                        hull = cv2.convexHull(cnt)
                        area_hull = cv2.contourArea(hull)
                        solidez = (area / area_hull) if area_hull > 0 else 0

                        if circularidad > CIRCULARIDAD_MINIMA and solidez > SOLIDEZ_MINIMA and len(cnt) >= 5:
                            try:
                                (x, y), (eje_menor, eje_mayor), angulo = cv2.fitEllipse(cnt)
                                relacion = (eje_mayor / eje_menor) if eje_menor > 0 else 0

                                if RELACION_EJES_MIN <= relacion <= RELACION_EJES_MAX:
                                    objeto_valido = True
                                    frames_validos_consecutivos += 1

                                    largo = eje_mayor / PIXELES_POR_CM
                                    ancho = eje_menor / PIXELES_POR_CM
                                    historial_largo.append(largo)
                                    historial_ancho.append(ancho)

                                    # Solo publicamos la medida como "confirmada" tras varios
                                    # frames seguidos validos, para no saltar con ruido puntual.
                                    if frames_validos_consecutivos >= FRAMES_CONSECUTIVOS_REQUERIDOS:
                                        l_cm = sum(historial_largo) / len(historial_largo)
                                        a_cm = sum(historial_ancho) / len(historial_ancho)

                                        v_elip = FACTOR_FORMA * l_cm * (a_cm ** 2)
                                        v_rev = v_elip * 0.98
                                        v_den = (peso_vivo / DENSIDAD_HUEVO) if peso_vivo > 0 else 0.0
                                        v_real = (v_elip + v_rev + v_den) / 3.0 if v_den > 0 else (v_elip + v_rev) / 2.0

                                        datos_globales.update({
                                            "largo": round(l_cm, 2), "ancho": round(a_cm, 2), "peso": round(peso_vivo, 1),
                                            "elipsoide": round(v_elip, 1), "revolucion": round(v_rev, 1), "bascula": round(v_den, 1),
                                            "volumen_real": round(v_real, 1), "categoria": clasificar_huevo(peso_vivo) if peso_vivo > 0 else "ESPERANDO..."
                                        })

                                    # El dibujo se hace siempre que se detecte forma, aunque aun
                                    # no este "confirmada", para dar feedback visual inmediato.
                                    cv2.ellipse(frame, ((x + offset_x, y + offset_y), (eje_menor, eje_mayor), angulo), (0, 255, 255), 2)
                                    texto = f"{datos_globales['categoria']} - {peso_vivo:.1f}g" if peso_vivo > 0 else "Detectado"
                                    cv2.putText(frame, texto, (int(x + offset_x) - 60, int(y + offset_y) - int(eje_mayor / 2) - 10),
                                                cv2.FONT_HERSHEY_SIMPLEX, 0.6, (0, 255, 255), 2)
                            except Exception:
                                pass

                        if USAR_ROI:
                            cv2.rectangle(frame, (ROI_X1, ROI_Y1), (ROI_X2, ROI_Y2), (100, 100, 100), 1)

                if not objeto_valido:
                    frames_validos_consecutivos = 0
                    historial_largo.clear()
                    historial_ancho.clear()
                    if peso_vivo <= 2.0:
                        datos_globales.update({
                            "largo": 0.0, "ancho": 0.0, "peso": 0.0,
                            "elipsoide": 0.0, "revolucion": 0.0, "bascula": 0.0,
                            "volumen_real": 0.0, "categoria": "ESPERANDO..."
                        })

                with lock_frame:
                    ok, buffer = cv2.imencode('.jpg', frame, [int(cv2.IMWRITE_JPEG_QUALITY), 35])
                    if ok:
                        frame_actual = buffer.tobytes()

            except Exception as e:
                # Cualquier error no anticipado en el loop se registra y el hilo sigue vivo
                # en la siguiente iteracion, en vez de morir en silencio.
                print(f"Error inesperado en el loop de vision (el hilo sigue activo): {e}")
                time.sleep(0.5)
                continue
    finally:
        # Se ejecuta SIEMPRE (error, ruptura del loop, excepcion no prevista) para no
        # dejar la camara reservada por el sistema operativo.
        try:
            cap.release()
        except Exception:
            pass
        cap_global = None
        print("Hilo de vision detenido, camara liberada.")


# ==========================================
# ENDPOINTS
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
def dispositivos_disponibles():
    """El WPF llama esto para mostrarle al usuario que puertos y camaras hay en ESTE PC."""
    return jsonify({
        "puertos": listar_puertos_disponibles(),
        "camaras": listar_camaras_disponibles()
    })


@app.route('/estado-configuracion', methods=['GET'])
def estado_configuracion():
    """El WPF llama esto al arrancar para saber si este PC ya tiene configuracion guardada
    o si debe mostrar la ventana de seleccion de hardware."""
    return jsonify(config_actual)


@app.route('/guardar-configuracion', methods=['POST'])
def guardar_configuracion():
    """El WPF llama esto despues de que el usuario elige puerto y camara en la ventana
    de configuracion. Los dispositivos se reconectan en caliente, sin reiniciar el proceso.
    Solo se fuerza la reconexion de un dispositivo si su valor realmente cambio, para no
    interrumpir innecesariamente la camara o la bascula cuando el usuario guarda sin
    modificar nada."""
    global config_actual, solicitud_reinicio_camara, solicitud_reinicio_bascula

    datos = request.get_json(force=True, silent=True) or {}
    puerto_nuevo = datos.get("puerto_bascula")
    camara_nueva = datos.get("camara_nombre")

    if puerto_nuevo != config_actual.get("puerto_bascula"):
        solicitud_reinicio_bascula = True
    if camara_nueva != config_actual.get("camara_nombre"):
        solicitud_reinicio_camara = True

    config_actual["puerto_bascula"] = puerto_nuevo
    config_actual["camara_nombre"] = camara_nueva
    config_actual["configurado"] = True
    guardar_config(config_actual)

    return jsonify({"ok": True, "mensaje": "Configuracion guardada. Reconectando dispositivos..."})


@app.route('/shutdown', methods=['POST'])
def shutdown():
    """Apagado ordenado: libera camara y bascula ANTES de morir. El WPF debe llamar esto
    al cerrarse, en vez de matar el proceso a la fuerza."""
    liberar_recursos()
    threading.Thread(target=lambda: (time.sleep(0.3), os._exit(0))).start()
    return jsonify({"ok": True}), 200

 # ==========================================
# ENDPOINTS DE CONTROL DE CÁMARA
# ==========================================

@app.route('/iniciar_camara', methods=['POST'])
def iniciar_camara_endpoint():
    return jsonify({"ok": True, "mensaje": "Cámara lista y transmitiendo"}), 200

@app.route('/detener_camara', methods=['POST'])
def detener_camara_endpoint():
    return jsonify({"ok": True, "mensaje": "Lectura de cámara en pausa"}), 200


if __name__ == '__main__':
    t = threading.Thread(target=loop_vision, daemon=True)
    t.start()
    app.run(host='0.0.0.0', port=5001, threaded=True)