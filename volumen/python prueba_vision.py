import cv2
import numpy as np
import math
import serial
import re
from collections import deque

# ==========================================
# 1. CONFIGURACIÓN DEL SISTEMA
# ==========================================
PIXELES_POR_CM = 54.0  
DENSIDAD_HUEVO = 1.07 
FACTOR_FORMA = 0.517  

PUERTO_BASCULA = '/dev/cu.usbserial-120' 
BAUDRATE = 9600 

# ==========================================
# 2. INICIALIZACIÓN DE HARDWARE
# ==========================================
try:
    bascula = serial.Serial(PUERTO_BASCULA, BAUDRATE, timeout=0.01)
    print(f"Conexión con báscula en {PUERTO_BASCULA}: OK")
except Exception as e:
    bascula = None
    print(f"ADVERTENCIA: Báscula no detectada.")

peso_actual_gramos = 0.0 

# ==========================================
# 3. FILTROS Y ESTABILIZACIÓN RÁPIDA
# ==========================================
historial_largo = deque(maxlen=15)
historial_ancho = deque(maxlen=15)
historial_peso = deque(maxlen=2) 

def estabilizar_medidas(largo_actual, ancho_actual):
    historial_largo.append(largo_actual)
    historial_ancho.append(ancho_actual)
    return sum(historial_largo) / len(historial_largo), sum(historial_ancho) / len(historial_ancho)

def leer_peso_bascula():
    global peso_actual_gramos
    if bascula and bascula.is_open:
        try:
            # Lectura directa del buffer sin esperar salto de línea estricto
            if bascula.in_waiting > 0:
                datos_raw = bascula.read_all().decode('utf-8', errors='ignore')
                if datos_raw:
                    numeros = re.findall(r'\d+\.\d+|\d+', datos_raw)
                    if numeros:
                        peso_crudo = float(numeros[-1]) # Agarramos el valor más reciente del flujo
                        if peso_crudo < 5.0: 
                            peso_crudo = peso_crudo * 1000 
                        
                        historial_peso.append(peso_crudo)
                        peso_actual_gramos = sum(historial_peso) / len(historial_peso)
        except Exception:
            pass
    return peso_actual_gramos

# ==========================================
# 4. CLASIFICACIÓN Y VOLÚMENES UNIFICADOS
# ==========================================
def clasificar_huevo(peso_g):
    if peso_g < 45.0:
        return "TIPO C"
    elif 45.0 <= peso_g < 53.0:
        return "TIPO B"
    elif 53.0 <= peso_g < 60.0:
        return "TIPO A"
    elif 60.0 <= peso_g < 67.0:
        return "TIPO AA"
    elif 67.0 <= peso_g < 78.0:
        return "TIPO AAA"
    else:
        return "JUMBO"

def calcular_volumenes_y_real(largo_cm, ancho_cm, peso_g):
    volumen_elipsoide = FACTOR_FORMA * largo_cm * (ancho_cm ** 2)
    volumen_revolucion = volumen_elipsoide * 0.98 
    volumen_bascula = (peso_g / DENSIDAD_HUEVO) if peso_g > 0 else 0.0
    
    if volumen_bascula > 0:
        volumen_real_unificado = (volumen_elipsoide + volumen_revolucion + volumen_bascula) / 3.0
    else:
        volumen_real_unificado = (volumen_elipsoide + volumen_revolucion) / 2.0
        
    return volumen_elipsoide, volumen_revolucion, volumen_bascula, volumen_real_unificado

# ==========================================
# 5. CICLO PRINCIPAL DE VISIÓN
# ==========================================
cap = cv2.VideoCapture(0)
cap.set(cv2.CAP_PROP_FRAME_WIDTH, 1280)
cap.set(cv2.CAP_PROP_FRAME_HEIGHT, 720)

print("Sistema de lectura serial forzada iniciado. Presiona 'q' para salir.")

while True:
    ret, frame = cap.read()
    if not ret: 
        break
    
    peso_vivo = leer_peso_bascula()
    categoria_actual = clasificar_huevo(peso_vivo) if peso_vivo > 0 else "ESPERANDO..."
    
    frame_blur = cv2.GaussianBlur(frame, (5, 5), 0)
    frame_filtrado = cv2.bilateralFilter(frame_blur, 9, 75, 75)
    hsv = cv2.cvtColor(frame_filtrado, cv2.COLOR_BGR2HSV)
    
    bajo_marron = np.array([0, 50, 90])
    alto_marron = np.array([30, 255, 255])
    mascara_marron = cv2.inRange(hsv, bajo_marron, alto_marron)
    
    bajo_blanco = np.array([0, 0, 200])
    alto_blanco = np.array([180, 30, 255])
    mascara_blanca = cv2.inRange(hsv, bajo_blanco, alto_blanco)
    
    mascara = cv2.bitwise_or(mascara_marron, mascara_blanca)
    
    kernel = cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (9, 9))
    mascara = cv2.morphologyEx(mascara, cv2.MORPH_OPEN, kernel, iterations=2)
    mascara = cv2.morphologyEx(mascara, cv2.MORPH_CLOSE, kernel, iterations=3)
    
    contornos, _ = cv2.findContours(mascara, cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_SIMPLE)
    
    if contornos:
        contorno_mas_grande = max(contornos, key=cv2.contourArea)
        if cv2.contourArea(contorno_mas_grande) > 2000:
            try:
                elipse_datos = cv2.fitEllipse(contorno_mas_grande)
                (x, y), (eje_menor_px, eje_mayor_px), angulo = elipse_datos
                
                if eje_menor_px > 0:
                    proporcion = eje_mayor_px / eje_menor_px
                    if 1.15 <= proporcion <= 1.7:
                        
                        cv2.ellipse(frame, elipse_datos, (0, 255, 0), 2)
                        
                        largo_bruto = eje_mayor_px / PIXELES_POR_CM
                        ancho_bruto = eje_menor_px / PIXELES_POR_CM
                        
                        largo_cm, ancho_cm = estabilizar_medidas(largo_bruto, ancho_bruto)
                        
                        v_elip, v_rev, v_den, v_real = calcular_volumenes_y_real(largo_cm, ancho_cm, peso_vivo)
                        
                        cv2.rectangle(frame, (10, 10), (480, 240), (0, 0, 0), -1)
                        cv2.putText(frame, f"Largo: {largo_cm:.2f} cm | Ancho: {ancho_cm:.2f} cm", (20, 35), cv2.FONT_HERSHEY_SIMPLEX, 0.55, (255, 255, 255), 2)
                        cv2.putText(frame, f"Peso USB: {peso_vivo:.1f} g | Cat: {categoria_actual}", (20, 68), cv2.FONT_HERSHEY_SIMPLEX, 0.55, (0, 255, 255), 2)
                        cv2.putText(frame, f"1. Elipsoide: {v_elip:.1f} cm3", (20, 105), cv2.FONT_HERSHEY_SIMPLEX, 0.6, (255, 150, 0), 2)
                        cv2.putText(frame, f"2. Revolucion: {v_rev:.1f} cm3", (20, 138), cv2.FONT_HERSHEY_SIMPLEX, 0.6, (0, 255, 0), 2)
                        cv2.putText(frame, f"3. Bascula: {v_den:.1f} cm3", (20, 171), cv2.FONT_HERSHEY_SIMPLEX, 0.6, (0, 0, 255), 2)
                        cv2.putText(frame, f"VOLUMEN REAL FINAL: {v_real:.1f} cm3", (20, 210), cv2.FONT_HERSHEY_SIMPLEX, 0.65, (255, 255, 0), 2)
            except Exception as e:
                pass

    cv2.imshow("Sistema Avicola - Clasificacion Total", frame)
    
    if cv2.waitKey(1) & 0xFF == ord('q'):
        break

cap.release()
if bascula:
    bascula.close()
cv2.destroyAllWindows()