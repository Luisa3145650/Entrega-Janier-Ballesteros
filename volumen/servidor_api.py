from flask import Flask, jsonify, Response
from flask_cors import CORS
import cv2
import numpy as np
import serial
import re
from collections import deque
import threading
import time

app = Flask(__name__)
CORS(app)

# ==========================================
# CONFIGURACIÓN DEL SISTEMA
# ==========================================
PIXELES_POR_CM = 54.0  
DENSIDAD_HUEVO = 1.07 
FACTOR_FORMA = 0.517  
PUERTO_BASCULA = 'COM7' 
BAUDRATE = 9600 

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

# --- NUEVO: buffer del último frame procesado, protegido por lock ---
frame_actual = None
lock_frame = threading.Lock()

try:
    bascula = serial.Serial(PUERTO_BASCULA, BAUDRATE, timeout=0.01)
    print(f"Puerto {PUERTO_BASCULA} abierto con éxito.")
except Exception as e:
    bascula = None
    print(f"Error abriendo puerto serial: {e}")

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
            
    # Watchdog independiente: Si la báscula no emite señales en 0.3 segundos, forzamos cero absoluto
    if time.time() - ultimo_timestamp_serial > 0.3:
        historial_peso.clear()
        peso_actual_memoria = 0.0
        
    return peso_actual_memoria

def loop_vision():
    global datos_globales, frame_actual
    cap = cv2.VideoCapture(0)
    cap.set(cv2.CAP_PROP_FRAME_WIDTH, 1280)
    cap.set(cv2.CAP_PROP_FRAME_HEIGHT, 720)
    
    while True:
        ret, frame = cap.read()
        if not ret: break
        
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

                        # Dibujar la elipse detectada sobre el frame para verla en /frame.jpg
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

        # --- NUEVO: codificar el frame (con dibujos incluidos) y guardarlo para servirlo por HTTP ---
        with lock_frame:
            ok, buffer = cv2.imencode('.jpg', frame, [int(cv2.IMWRITE_JPEG_QUALITY), 70])
            if ok:
                frame_actual = buffer.tobytes()

@app.route('/datos-huevo', methods=['GET'])
def get_datos():
    return jsonify(datos_globales)

# --- NUEVO: endpoint que sirve el último frame procesado como JPEG ---
@app.route('/frame.jpg', methods=['GET'])
def get_frame():
    with lock_frame:
        if frame_actual is None:
            return '', 204
        return Response(frame_actual, mimetype='image/jpeg')

if __name__ == '__main__':
    t = threading.Thread(target=loop_vision, daemon=True)
    t.start()
    app.run(host='0.0.0.0', port=5001)