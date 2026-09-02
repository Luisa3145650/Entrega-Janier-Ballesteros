"""
server.py - Proyecto deteccion-huevos
---------------------------------------
Detecta el huevo con el modelo entrenado (best.pt, YOLOv8-seg), mide su
largo y diámetro reales usando el contorno detectado + la escala calibrada
con la hoja milimetrada, calcula el volumen con 3 fórmulas distintas y
clasifica el huevo por categoría (peso + volumen) según la NTC 1240:2011.

Cómo correrlo:
    uvicorn server:app --host 127.0.0.1 --port 8020   (para el proyecto Nest)
    uvicorn server:app --host 127.0.0.1 --port 8021   (para el proyecto C#)

(Corre dos copias de esta misma carpeta, una por proyecto, cada una con su
 propio escala_config.json si las cámaras son distintas - mismo patrón que
 ya usas en ZonaAvicola con los puertos 8000/8001.)
"""

import base64
import json
import math
import os
from typing import Optional

import cv2
import numpy as np
from fastapi import FastAPI, File, UploadFile
from fastapi.middleware.cors import CORSMiddleware
from pydantic import BaseModel
from ultralytics import YOLO

app = FastAPI()
app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],
    allow_methods=["*"],
    allow_headers=["*"],
)

# ---------------------------------------------------------------------
# Modelo entrenado (best.pt) - se carga una sola vez al arrancar
# ---------------------------------------------------------------------
RUTA_MODELO = os.path.join(os.path.dirname(__file__), "modelo", "best.pt")
modelo = YOLO(RUTA_MODELO)

# ---------------------------------------------------------------------
# Escala (px por cm) - calibrada con calibrar_escala.py sobre la hoja
# milimetrada. Si todavía no se ha calibrado, usa el fallback.
# ---------------------------------------------------------------------
RUTA_ESCALA = os.path.join(os.path.dirname(__file__), "escala_config.json")
PX_POR_CM_FALLBACK = 35.0


def leer_escala_actual() -> float:
    try:
        with open(RUTA_ESCALA, "r") as f:
            datos = json.load(f)
            return float(datos.get("px_por_cm", PX_POR_CM_FALLBACK))
    except (FileNotFoundError, json.JSONDecodeError):
        return PX_POR_CM_FALLBACK


# ---------------------------------------------------------------------
# Tabla oficial NTC 1240:2011 / FENAVI (peso en g, volumen en cm3)
# ---------------------------------------------------------------------
CATEGORIAS = [
    ("C",     0.0,  46.0,   0.0,  42.3),
    ("B",     46.0, 52.9,   42.3, 48.7),
    ("A",     53.0, 59.9,   48.8, 55.1),
    ("AA",    60.0, 66.9,   55.2, 61.6),
    ("AAA",   67.0, 77.9,   61.7, 71.8),
    ("Jumbo", 78.0, 120.0,  71.8, 110.0),
]


def _distancia_normalizada(valor: float, minimo: float, maximo: float) -> float:
    ancho = max(maximo - minimo, 1e-6)
    if minimo <= valor <= maximo:
        return 0.0
    return (minimo - valor) / ancho if valor < minimo else (valor - maximo) / ancho


def clasificar_huevo(peso_g: float, volumen_cm3: float, peso_valido: bool) -> dict:
    mejor_categoria, mejor_score = None, float("inf")
    for nombre, p_min, p_max, v_min, v_max in CATEGORIAS:
        d_peso = _distancia_normalizada(peso_g, p_min, p_max) if peso_valido else 0.0
        d_vol = _distancia_normalizada(volumen_cm3, v_min, v_max)
        score = (d_peso + d_vol) if peso_valido else d_vol
        if score < mejor_score:
            mejor_score, mejor_categoria = score, nombre
    return {
        "categoria": mejor_categoria,
        "confianza": "peso_y_volumen" if peso_valido else "volumen",
    }


# ---------------------------------------------------------------------
# Las 3 fórmulas de volumen
# ---------------------------------------------------------------------

def volumen_elipsoide(largo_cm: float, diametro_cm: float) -> float:
    a, b = largo_cm / 2.0, diametro_cm / 2.0
    return (4.0 / 3.0) * math.pi * a * (b ** 2)


def volumen_por_contorno(mascara_px: np.ndarray, px_por_cm: float, n_discos: int = 60) -> float:
    """Volumen real por el contorno de la máscara (teorema de Pappus)."""
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


def volumen_narushin(largo_cm: float, diametro_cm: float) -> float:
    return 0.51 * largo_cm * (diametro_cm ** 2)


# ---------------------------------------------------------------------
# Endpoints
# ---------------------------------------------------------------------

@app.get("/ping")
def ping():
    return {"status": "ok"}


@app.get("/escala")
def escala():
    return {"px_por_cm": leer_escala_actual()}


def procesar_frame(frame: np.ndarray, peso_g: Optional[float], n_puntos_contorno: int = 60) -> dict:
    """
    Logica central de deteccion, compartida por /clasificar (multipart, usado
    por Nest) y /detectar-vivo (JSON, usado por el loop en vivo de Angular).
    Devuelve tambien el CONTORNO REAL (poligono que YOLO detecto), no solo
    la elipse aproximada, para poder dibujarlo tal cual en el frontend.
    """
    px_por_cm = leer_escala_actual()

    resultados = modelo(frame, verbose=False)
    if not resultados or resultados[0].masks is None or len(resultados[0].masks.data) == 0:
        return {"huevo_detectado": False}

    mascara_modelo = resultados[0].masks.data[0].cpu().numpy()
    alto_frame, ancho_frame = frame.shape[:2]
    mascara_px = cv2.resize(mascara_modelo, (ancho_frame, alto_frame), interpolation=cv2.INTER_NEAREST)
    mascara_px = (mascara_px > 0.5).astype(np.uint8)

    contornos, _ = cv2.findContours(mascara_px, cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_NONE)
    if not contornos:
        return {"huevo_detectado": False}
    contorno_principal = max(contornos, key=cv2.contourArea)

    if len(contorno_principal) < 5:
        return {"huevo_detectado": False}

    elipse = cv2.fitEllipse(contorno_principal)
    (cx, cy), (ancho_px, alto_px), angulo = elipse
    eje_mayor_px = max(ancho_px, alto_px)
    eje_menor_px = min(ancho_px, alto_px)

    largo_cm = eje_mayor_px / px_por_cm
    diametro_cm = eje_menor_px / px_por_cm

    v1 = volumen_elipsoide(largo_cm, diametro_cm)
    v2 = volumen_por_contorno(mascara_px, px_por_cm)
    v3 = volumen_narushin(largo_cm, diametro_cm)
    valores_validos = [v for v in (v1, v2, v3) if v is not None]
    volumen_promedio = sum(valores_validos) / len(valores_validos)

    peso_valido = peso_g is not None and peso_g > 0
    clasificacion = clasificar_huevo(peso_g if peso_valido else 0.0, volumen_promedio, peso_valido)

    # Reducir el contorno (puede traer cientos de puntos) a ~n_puntos_contorno
    # para que el payload sea liviano y se pueda dibujar cada ~800ms sin
    # saturar la conexion. cv2.approxPolyDP conserva la forma real del huevo.
    perimetro = cv2.arcLength(contorno_principal, True)
    epsilon = 0.002 * perimetro
    contorno_simplificado = cv2.approxPolyDP(contorno_principal, epsilon, True)
    puntos_contorno = [[int(p[0][0]), int(p[0][1])] for p in contorno_simplificado]

    return {
        "huevo_detectado": True,
        "largo_cm": round(largo_cm, 2),
        "diametro_cm": round(diametro_cm, 2),
        "volumen_elipsoide_cm3": round(v1, 2),
        "volumen_contorno_cm3": round(v2, 2) if v2 is not None else None,
        "volumen_narushin_cm3": round(v3, 2),
        "volumen_promedio_cm3": round(volumen_promedio, 2),
        "categoria": clasificacion["categoria"],
        "clasificado_por": clasificacion["confianza"],
        "contorno": puntos_contorno,
        "elipse": {
            "cx": round(cx, 1),
            "cy": round(cy, 1),
            "ancho_px": round(ancho_px, 1),
            "alto_px": round(alto_px, 1),
            "angulo_deg": round(angulo, 1),
        },
        "_frame": frame,
        "_contorno_cv": contorno_principal,
        "_elipse_cv": elipse,
    }


@app.post("/clasificar")
async def clasificar(imagen: UploadFile = File(...), peso_g: float = -1):
    """
    Recibe una foto del huevo (multipart/form-data, campo 'imagen').
    Usado por Nest (deteccion-huevo.service.ts) para la captura puntual
    con imagen anotada en base64. peso_g es opcional (query param).
    """
    contenido = await imagen.read()
    array_np = np.frombuffer(contenido, dtype=np.uint8)
    frame = cv2.imdecode(array_np, cv2.IMREAD_COLOR)

    if frame is None:
        return {"error": "No se pudo leer la imagen enviada"}

    peso_valido = peso_g is not None and peso_g > 0
    resultado = procesar_frame(frame, peso_g if peso_valido else None)

    if not resultado.get("huevo_detectado"):
        return {"huevo_detectado": False}

    contorno_principal = resultado.pop("_contorno_cv")
    elipse = resultado.pop("_elipse_cv")
    frame_local = resultado.pop("_frame")
    (cx, cy), (ancho_px, alto_px), angulo = elipse

    frame_anotado = frame_local.copy()
    cv2.drawContours(frame_anotado, [contorno_principal], -1, (0, 255, 255), 2)
    cv2.ellipse(frame_anotado, elipse, (0, 255, 0), 2)
    cv2.putText(frame_anotado, f"{resultado['largo_cm']:.2f}cm x {resultado['diametro_cm']:.2f}cm",
                (int(cx - 60), int(cy - alto_px / 2 - 10)),
                cv2.FONT_HERSHEY_SIMPLEX, 0.6, (0, 255, 255), 2)
    _, buffer = cv2.imencode(".jpg", frame_anotado)
    resultado["frame_anotado"] = base64.b64encode(buffer).decode("utf-8")

    return resultado


class FrameVivoRequest(BaseModel):
    frame: str  # data URL: "data:image/jpeg;base64,...."
    peso_g: Optional[float] = None


@app.post("/detectar-vivo")
async def detectar_vivo(payload: FrameVivoRequest):
    """
    Version liviana para el loop en vivo de Angular (se llama cada ~800ms).
    Recibe el frame como JSON (mismo formato que ya usas contra el 8001) y
    devuelve el CONTORNO REAL detectado por YOLO (lista de puntos [x,y] en
    pixeles del frame recibido) para dibujarlo tal cual sobre el canvas,
    en vez de una elipse aproximada. No devuelve frame_anotado (mas rapido).
    """
    try:
        data_url = payload.frame
        if "," in data_url:
            data_url = data_url.split(",", 1)[1]
        contenido = base64.b64decode(data_url)
        array_np = np.frombuffer(contenido, dtype=np.uint8)
        frame = cv2.imdecode(array_np, cv2.IMREAD_COLOR)
    except Exception:
        return {"huevo_detectado": False, "error": "No se pudo decodificar el frame"}

    if frame is None:
        return {"huevo_detectado": False, "error": "No se pudo decodificar el frame"}

    peso_valido = payload.peso_g is not None and payload.peso_g > 0
    resultado = procesar_frame(frame, payload.peso_g if peso_valido else None)

    resultado.pop("_frame", None)
    resultado.pop("_contorno_cv", None)
    resultado.pop("_elipse_cv", None)
    return resultado