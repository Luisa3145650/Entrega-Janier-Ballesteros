# Módulo de Entrenamiento del Modelo de Detección (YOLOv8-seg)

Este directorio contiene el script y la documentación necesarios para re-entrenar la red neuronal YOLOv8-seg encargada de detectar y segmentar huevos en tiempo real.

---

## 📋 Prerrequisitos y Configuración de Variables de Entorno

1. **Clave API de Roboflow (Obligatorio):**
   Es necesario configurar la variable de entorno `ROBOFLOW_API_KEY` con tu clave privada de Roboflow antes de ejecutar el entrenamiento.

   - **PowerShell (Windows):**
     ```powershell
     $env:ROBOFLOW_API_KEY="tu_api_key_aqui"
     ```
   - **CMD (Windows):**
     ```cmd
     set ROBOFLOW_API_KEY=tu_api_key_aqui
     ```
   - **Bash / Linux / macOS:**
     ```bash
     export ROBOFLOW_API_KEY="tu_api_key_aqui"
     ```

2. **Versión del Dataset en Roboflow (Opcional, Default = 1):**
   Puedes especificar cuál versión del dataset descargará el script mediante la variable `ROBOFLOW_DATASET_VERSION`.

   - **PowerShell (Windows):**
     ```powershell
     $env:ROBOFLOW_DATASET_VERSION="2"
     ```
   - **CMD (Windows):**
     ```cmd
     set ROBOFLOW_DATASET_VERSION=2
     ```
   - **Bash / Linux / macOS:**
     ```bash
     export ROBOFLOW_DATASET_VERSION="2"
     ```

3. **Instalación de Dependencias:**
   ```bash
   pip install roboflow ultralytics pycocotools opencv-python numpy
   ```

---

## 🚀 ¿Cuándo Re-entrenar el Modelo?

Se recomienda ejecutar un nuevo entrenamiento en los siguientes escenarios:
1. **Nuevas Condiciones de Iluminación o Cámara:** Si se instala una nueva cámara o la iluminación de la galera cambia drásticamente.
2. **Nuevas Variedades o Tamaños de Huevos:** Si se incorporan razas o tipos de huevos con tonalidades o formas no cubiertas en el dataset original.
3. **Optimización de Precisión:** Si se han etiquetado y aprobado nuevas imágenes en el proyecto de Roboflow.

---

## 📦 Gestión del Dataset en Roboflow

- **Workspace:** `sofia-bolanos`
- **Proyecto:** `deteccion-huevos`
- **Formato de Exportación:** COCO Segmentation / YOLOv8 Segmentation.
- **Procedimiento en Roboflow:**
  1. Subir las nuevas fotografías del huevo sobre el fondo de medición.
  2. Etiquetar o ajustar las máscaras de contorno usando las herramientas de Smart Polygon / SAM 3.
  3. Generar una nueva versión en Roboflow (ej. Versión 2).
  4. Configurar la versión deseada (`ROBOFLOW_DATASET_VERSION=2`) antes de ejecutar el entrenamiento.

---

## 🔄 Procedimiento Automatizado para Entrenar y Actualizar `best.pt`

1. **Ejecutar el script de entrenamiento:**
   ```bash
   python scripts/entrenar_huevos.py
   ```

2. **Copia y Respaldo Automático:**
   Al finalizar el entrenamiento (80 épocas con resolución 640x640), el script realiza automáticamente las siguientes acciones:
   - Identifica la ruta exacta del modelo generado (`resultados.save_dir / "weights" / "best.pt"`).
   - Genera un respaldo del modelo anterior en `volumen/modelo/best.pt.backup`.
   - Copia automáticamente el nuevo modelo entrenado a `volumen/modelo/best.pt`.

3. **Reiniciar el Servidor de Visión:**
   Al reiniciar la aplicación WPF o el servidor Python (`volumen/servidor_api.py`), los nuevos pesos serán cargados automáticamente en memoria.
