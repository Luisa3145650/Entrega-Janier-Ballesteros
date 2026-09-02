# -*- coding: utf-8 -*-
"""
entrenar_huevos.py
------------------
Script de entrenamiento automatizado para el modelo de detección y segmentación
de huevos (YOLOv8-seg) utilizando Roboflow y Ultralytics.

Este script:
1. Descarga el dataset etiquetado desde Roboflow (formato COCO Segmentation).
2. Convierte las máscaras COCO/RLE al formato YOLOv8-seg.
3. Genera la estructura de directorios y data.yaml.
4. Entrena YOLOv8n-seg durante 80 épocas.
5. Exporta el archivo de pesos óptimos (best.pt) y realiza backup automático en volumen/modelo/best.pt.
"""

import json
import os
import shutil
import sys
import cv2
import numpy as np

# 1) Verificar e instalar librerías necesarias si no existen
try:
    from roboflow import Roboflow
    from ultralytics import YOLO
    from pycocotools import mask as maskUtils
except ImportError as e:
    print("⚠️ Falta alguna librería requerida:", e)
    print("Ejecuta: pip install roboflow ultralytics pycocotools -q")
    sys.exit(1)

# 2) Obtener API Key y versión de Dataset desde variables de entorno
api_key = os.environ.get("ROBOFLOW_API_KEY")
if not api_key:
    print("❌ ERROR: La variable de entorno ROBOFLOW_API_KEY no está configurada.")
    print("Configúrala antes de ejecutar el entrenamiento:")
    print("  Windows PowerShell: $env:ROBOFLOW_API_KEY='tu_key'")
    print("  Windows CMD:        set ROBOFLOW_API_KEY=tu_key")
    print("  Linux/macOS:        export ROBOFLOW_API_KEY='tu_key'")
    print("\nOpcional: puedes configurar la versión del dataset (default 1):")
    print("  Windows PowerShell: $env:ROBOFLOW_DATASET_VERSION='2'")
    print("  Windows CMD:        set ROBOFLOW_DATASET_VERSION=2")
    print("  Linux/macOS:        export ROBOFLOW_DATASET_VERSION='2'")
    sys.exit(1)

version_numero = int(os.environ.get("ROBOFLOW_DATASET_VERSION", "1"))

# 3) Descargar Dataset desde Roboflow con manejo de errores
try:
    rf = Roboflow(api_key=api_key)
    project = rf.workspace("sofia-bolanos").project("deteccion-huevos")
    version = project.version(version_numero)
    dataset = version.download("coco-segmentation")
    print(f"✅ Dataset v{version_numero} descargado en:", dataset.location)
except Exception as e:
    print(f"❌ Error al conectar o descargar el dataset desde Roboflow (Versión {version_numero}): {e}")
    print("Verifica tu ROBOFLOW_API_KEY, la versión del dataset y tu conexión a internet.")
    sys.exit(1)

# 4) Convertir formato COCO Segmentation -> YOLOv8-seg
DATASET_DIR = dataset.location
SPLITS = ["train", "valid", "test"]

def poligono_desde_rle(rle, alto, ancho):
    """Convierte una máscara RLE (píxeles) en un contorno (lista de puntos x,y)."""
    if isinstance(rle["counts"], list):
        rle = maskUtils.frPyObjects(rle, alto, ancho)
    mascara = maskUtils.decode(rle)
    contornos, _ = cv2.findContours(mascara.astype(np.uint8), cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_SIMPLE)
    if not contornos:
        return None
    contorno_mas_grande = max(contornos, key=cv2.contourArea)
    return contorno_mas_grande.reshape(-1, 2).flatten().tolist()

def convertir_split_coco_a_yolo(split):
    carpeta_split = os.path.join(DATASET_DIR, split)
    ruta_json = os.path.join(carpeta_split, "_annotations.coco.json")
    if not os.path.exists(ruta_json):
        print(f"Aviso: no se encontró {ruta_json}, se omite el split '{split}'")
        return None

    with open(ruta_json, "r", encoding="utf-8") as f:
        coco = json.load(f)

    imagenes = {img["id"]: img for img in coco["images"]}
    categorias = {cat["id"]: i for i, cat in enumerate(coco["categories"])}

    carpeta_labels = os.path.join(carpeta_split, "labels")
    os.makedirs(carpeta_labels, exist_ok=True)

    anotaciones_por_imagen = {}
    for ann in coco["annotations"]:
        anotaciones_por_imagen.setdefault(ann["image_id"], []).append(ann)

    contador = 0
    total_instancias = 0
    for id_img, info_img in imagenes.items():
        ancho = info_img["width"]
        alto = info_img["height"]
        nombre_base = os.path.splitext(info_img["file_name"])[0]
        ruta_txt = os.path.join(carpeta_labels, nombre_base + ".txt")

        lineas = []
        for ann in anotaciones_por_imagen.get(id_img, []):
            clase = categorias[ann["category_id"]]
            segmentacion = ann.get("segmentation", [])

            puntos_planos = None
            if isinstance(segmentacion, dict):
                puntos_planos = poligono_desde_rle(segmentacion, alto, ancho)
            elif isinstance(segmentacion, list) and len(segmentacion) > 0:
                puntos_planos = segmentacion[0]

            if not puntos_planos or len(puntos_planos) < 6:
                continue

            coords_norm = []
            for i in range(0, len(puntos_planos), 2):
                x = puntos_planos[i] / ancho
                y = puntos_planos[i + 1] / alto
                coords_norm.extend([round(x, 6), round(y, 6)])
            lineas.append(f"{clase} " + " ".join(str(c) for c in coords_norm))
            total_instancias += 1

        with open(ruta_txt, "w", encoding="utf-8") as f_out:
            f_out.write("\n".join(lineas))

        contador += 1

    print(f"{split}: {contador} imágenes procesadas, {total_instancias} huevos etiquetados")
    return contador, categorias

nombres_clases = None
for split in SPLITS:
    resultado = convertir_split_coco_a_yolo(split)
    if resultado:
        n, categorias = resultado
        if nombres_clases is None:
            with open(os.path.join(DATASET_DIR, split, "_annotations.coco.json"), "r", encoding="utf-8") as f:
                cats_json = json.load(f)["categories"]
            nombres_clases = [None] * len(cats_json)
            for cat in cats_json:
                nombres_clases[categorias[cat["id"]]] = cat["name"]

if nombres_clases is None:
    print("⚠️ No se pudieron determinar las clases desde los splits, usando fallback ['huevo']")
    nombres_clases = ["huevo"]

print("Clases detectadas:", nombres_clases)

# 5) Crear archivo data.yaml
for split in SPLITS:
    carpeta_split = os.path.join(DATASET_DIR, split)
    if not os.path.isdir(carpeta_split):
        continue
    carpeta_images = os.path.join(carpeta_split, "images")
    os.makedirs(carpeta_images, exist_ok=True)

    extensiones = (".jpg", ".jpeg", ".png")
    for nombre_archivo in os.listdir(carpeta_split):
        ruta_origen = os.path.join(carpeta_split, nombre_archivo)
        if os.path.isfile(ruta_origen) and nombre_archivo.lower().endswith(extensiones):
            shutil.move(ruta_origen, os.path.join(carpeta_images, nombre_archivo))

    print(f"{split}: fotos movidas a {carpeta_images}")

yaml_contenido = f"""train: {DATASET_DIR}/train/images
val: {DATASET_DIR}/valid/images
test: {DATASET_DIR}/test/images

nc: {len(nombres_clases)}
names: {nombres_clases}
"""

ruta_yaml = os.path.join(DATASET_DIR, "data.yaml")
with open(ruta_yaml, "w", encoding="utf-8") as f:
    f.write(yaml_contenido)

print("✅ data.yaml creado en:", ruta_yaml)

# 6) Entrenar el modelo YOLOv8-seg con exist_ok=True y manejo de excepciones
try:
    print("🚀 Iniciando entrenamiento de YOLOv8n-seg...")
    modelo = YOLO("yolov8n-seg.pt")

    resultados = modelo.train(
        data=ruta_yaml,
        epochs=80,
        imgsz=640,
        batch=16,
        name="deteccion_huevos",
        exist_ok=True
    )
    ruta_best_entrenado = str(resultados.save_dir / "weights" / "best.pt")
except Exception as e:
    print(f"❌ Error durante el entrenamiento del modelo YOLOv8: {e}")
    sys.exit(1)

print(f"🎉 Entrenamiento finalizado. Modelo guardado en: {ruta_best_entrenado}")

# 7) Copia automática a volumen/modelo/best.pt con backup previo
destino = os.path.join("volumen", "modelo", "best.pt")
os.makedirs(os.path.dirname(destino), exist_ok=True)

if os.path.exists(destino):
    backup_path = destino + ".backup"
    shutil.copy(destino, backup_path)
    print(f"📦 Backup del modelo anterior guardado en: {backup_path}")

shutil.copy(ruta_best_entrenado, destino)
print(f"✅ Modelo copiado automáticamente a: {destino}")
