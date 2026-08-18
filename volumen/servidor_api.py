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

app = Flask(__name__)
CORS(app)

# ==========================================
# CONFIGURACIÓN DEL SISTEMA Y PERSISTENCIA
# ==========================================
PIXELES_POR_CM = 54.0  
DENSIDAD_HUEVO = 1.07 
FACTOR_FORMA = 0.517  
BAUDRATE = 9600 

CONFIG_DIR = os.path.join(os.environ.get('PROGRAMDATA', 'C:\\ProgramData'), 'ClasificadorHuevos')
CONFIG_FILE = os.path.join(CONFIG_DIR, 'config.json')

PUERTO_BASCULA = ''
CAMARA_INDEX = 0
CAMARA_NOMBRE = ''
CONFIGURADO = False

lock_hardware = threading.Lock()
pausa_vision = False
vision_corriendo = True

bascula = None
cap = None

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

frame_actual = None
lock_frame = threading.Lock()

historial_largo = deque(maxlen=5)  
historial_ancho = deque(maxlen=5)  
historial_peso = deque(maxlen=2) 

peso_actual_memoria = 0.0
ultimo_timestamp_serial = time.time()

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
                print(f"ℹ️ Configuración previa cargada: Puerto='{PUERTO_BASCULA}', Cámara={CAMARA_INDEX}, Configurado={CONFIGURADO}")
        except Exception as e:
            print(f"⚠️ Error cargando config.json: {e}")

def abrir_hardware():
    global bascula, cap, PUERTO_BASCULA, CAMARA_INDEX, CONFIGURADO
    
    # Cerrar báscula previa
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

    # Cerrar cámara previa
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
                cap = cap_temp
                print(f"✅ Cámara {CAMARA_INDEX} abierta con éxito.")
            else:
                print(f"⚠️ No se pudo abrir la cámara {CAMARA_INDEX}.")
        except Exception as e:
            cap = None
            print(f"⚠️ Error abriendo cámara {CAMARA_INDEX}: {e}")

def clasificar_huevo(peso_g):
    if peso_g < 45.0: return "TIPO C"
    elif 45.0 <= peso_g < 53.0: return "TIPO B"
    elif 53.0 <= peso_g < 60.0: return "TIPO A"
    elif 60.0 <= peso_g < 67.0: return "TIPO AA"
    elif 67.0 <= peso_g < 78.0: return "TIPO AAA"
    else: return "JUMBO"

def leer_peso_bascula():
    global peso_actual_memoria, ultimo_timestamp_serial
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
        except:
            pass
            
    if time.time() - ultimo_timestamp_serial > 0.3:
        historial_peso.clear()
        peso_actual_memoria = 0.0
        
    return peso_actual_memoria

def loop_vision():
    global datos_globales, frame_actual, cap, bascula, pausa_vision, vision_corriendo
    
    with lock_hardware:
        abrir_hardware()

    while vision_corriendo:
        if pausa_vision:
            time.sleep(0.1)
            continue

        with lock_hardware:
            if cap is None or not cap.isOpened():
                time.sleep(0.1)
                continue
            
            ret, frame = cap.read()
            if not ret or frame is None:
                time.sleep(0.05)
                continue
            
            peso_vivo = leer_peso_bascula()

            hsv = cv2.cvtColor(cv2.bilateralFilter(cv2.GaussianBlur(frame, (5, 5), 0), 9, 75, 75), cv2.COLOR_BGR2HSV)
            mascara = cv2.bitwise_or(cv2.inRange(hsv, np.array([0, 50, 90]), np.array([30, 255, 255])),
                                     cv2.inRange(hsv, np.array([0, 0, 200]), np.array([180, 30, 255])))
            kernel = cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (9, 9))
            mascara = cv2.morphologyEx(cv2.morphologyEx(mascara, cv2.MORPH_OPEN, kernel, iterations=2), cv2.MORPH_CLOSE, kernel, iterations=3)
            
            contornos, _ = cv2.findContours(mascara, cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_SIMPLE)
            
            objeto_valido = False
            if contornos:
                cnt = max(contornos, key=cv2.contourArea)
                if cv2.contourArea(cnt) > 2000:
                    try:
                        (x, y), (eje_menor, eje_mayor), angulo = cv2.fitEllipse(cnt)
                        if eje_menor > 0 and 1.15 <= (eje_mayor / eje_menor) <= 1.7:
                            objeto_valido = True
                            largo = eje_mayor / PIXELES_POR_CM
                            ancho = eje_menor / PIXELES_POR_CM
                            historial_largo.append(largo)
                            historial_ancho.append(ancho)
                            
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

                            cv2.ellipse(frame, ((x, y), (eje_menor, eje_mayor), angulo), (0, 255, 255), 2)
                            texto = f"{datos_globales['categoria']} - {peso_vivo:.1f}g" if peso_vivo > 0 else "Detectado"
                            cv2.putText(frame, texto, (int(x) - 60, int(y) - int(eje_mayor / 2) - 10),
                                        cv2.FONT_HERSHEY_SIMPLEX, 0.6, (0, 255, 255), 2)
                    except: pass

            if not objeto_valido or peso_vivo <= 2.0:
                historial_largo.clear()
                historial_ancho.clear()
                if peso_vivo <= 2.0:
                    datos_globales.update({
                        "largo": 0.0, "ancho": 0.0, "peso": 0.0,
                        "elipsoide": 0.0, "revolucion": 0.0, "bascula": 0.0,
                        "volumen_real": 0.0, "categoria": "ESPERANDO..."
                    })

            with lock_frame:
                ok, buffer = cv2.imencode('.jpg', frame, [int(cv2.IMWRITE_JPEG_QUALITY), 70])
                if ok:
                    frame_actual = buffer.tobytes()

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
    puertos_list = []
    try:
        ports = serial.tools.list_ports.comports()
        for p in ports:
            desc = f"{p.device}"
            if p.description and p.description != "n/a":
                desc += f" - {p.description}"
            puertos_list.append({
                "puerto": p.device,
                "descripcion": desc
            })
    except Exception as e:
        print(f"Error listando puertos COM: {e}")

    camaras_list = []
    for idx in range(4):
        # Si la cámara actual es la que está abierta en cap, la damos por disponible directamente
        if idx == CAMARA_INDEX and cap is not None and cap.isOpened():
            camaras_list.append({
                "id": idx,
                "nombre": f"Cámara {idx} (Principal)"
            })
            continue

        try:
            test_cap = cv2.VideoCapture(idx, cv2.CAP_DSHOW)
            if not test_cap.isOpened():
                test_cap = cv2.VideoCapture(idx)
            
            if test_cap.isOpened():
                ret, _ = test_cap.read()
                test_cap.release()
                if ret or test_cap.isOpened():
                    camaras_list.append({
                        "id": idx,
                        "nombre": f"Cámara {idx}"
                    })
        except:
            pass

    return jsonify({
        "puertos": puertos_list,
        "camaras": camaras_list
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

    # Pausar vision para evitar carrera de hardware
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
    time.sleep(0.1)
    with lock_hardware:
        if cap and cap.isOpened():
            try: cap.release()
            except: pass
        if bascula and bascula.is_open:
            try: bascula.close()
            except: pass
    func = request.environ.get('werkzeug.server.shutdown')
    if func:
        func()
    return jsonify({"status": "ok", "message": "Servidor apaga la sesión limpiamente"})

if __name__ == '__main__':
    cargar_config_file()
    t = threading.Thread(target=loop_vision, daemon=True)
    t.start()
    app.run(host='0.0.0.0', port=5001)
