# Servidor de Visión Artificial y Calibración (`volumen/servidor_api.py`)

Este módulo implementa el servidor en Python (Flask puerto `5001`) encargado de procesar la captura de video en tiempo real, ejecutar el modelo de segmentación de IA YOLOv8-seg (con fallback HSV), calcular el volumen mediante 3 fórmulas geométricas, comunicarse con la báscula serial USB y servir los endpoints REST consumidos por la interfaz C# WPF.

---

## 🛠️ Variables de Entorno y Configuración Avanzada

El comportamiento del servidor se puede personalizar en producción sin modificar código fuente mediante las siguientes variables de entorno:

### 1. Control de Dispositivo e Inferencia de IA
- **`YOLO_DEVICE`**: Dispositivo para la ejecución de la red neuronal.
  - Valores: `"cpu"` (defecto), `"cuda"` o `"0"` (para aceleración por GPU NVIDIA).
- **`YOLO_INFERENCE_INTERVAL_SEC`**: Frecuencia del hilo asíncrono de inferencia en segundos.
  - Valor por defecto: `"0.1"` (~10 inferencias/segundo).

### 2. Filtrado Morfológico HSV (Combate de Parpadeo por Luces LED)
Bajo iluminación LED (paneles/tiras con flickering PWM), las bandas negras del *rolling shutter* pueden cortar la máscara del huevo. Se pueden ajustar los kernels de apertura y cierre morfológico:
- **`HSV_KERNEL_OPEN`**: Tamaño del kernel para eliminación de ruido de fondo (defecto `"9"`).
- **`HSV_KERNEL_CLOSE`**: Tamaño del kernel para rellenar huecos y bandas negras en el contorno del huevo (defecto `"17"`).

### 3. Calibración Óptica y Escala Espacial (Píxeles por Centímetro)
- **`PIXELES_POR_CM`**: Factor de conversión espacial para traducir dimensiones medidas en píxeles a centímetros en el plano del plato de la báscula.
  - Valor por defecto: `"57.0"` (Calibrado físicamente el 22/08/2026).
  - **Detalles de la Calibración Física (22/08/2026):**
    - Se utilizó una moneda colombiana de $100 COP (diámetro exacto conocido: 2,3 cm) colocada exactamente sobre el plato de la báscula.
    - La distancia fija entre la lente de la cámara y la superficie de la báscula fue de 25 cm.
    - Medición en el frame capturado: `diámetro en píxeles / 2,3 cm ≈ 57,0 px/cm`.
    - Esta calibración redujo el margen de error de estimación de volumen de ~11-12% a menos de 3-4% respecto al volumen teórico por densidad (`peso_g / 1,07`).
  - **⚠️ Importante sobre la Recalibración:**
    Si en el futuro se altera la posición o altura física de la cámara respecto a la báscula (distancia de 25 cm), este valor **debe recalibrarse obligatoriamente** colocando la misma moneda de $100 COP (o un patrón de 2,3 cm) sobre la báscula, midiendo su diámetro en píxeles y actualizando la variable `PIXELES_POR_CM`.

### 4. Ajuste de Exposición Manual de Cámara (Opcional)
Para evitar que el auto-exposure de la webcam parpadee intentando compensar paneles LED en tiempo real:
- **`CAMARA_EXPOSURE_MANUAL`**: `"1"` para activar exposición manual; `"0"` para mantener auto-exposición por defecto (defecto `"0"`).
- **`CAMARA_EXPOSURE_VALOR`**: Valor de exposición en escala logarítmica de DirectShow (defecto `"-6"`).

---

## 💻 Instrucciones de Activación en PowerShell (Windows)

Para activar el control manual de exposición y ajustar los kernels morfológicos antes de iniciar la aplicación:

```powershell
# Activar exposición manual de cámara y fijar valor de exposición
$env:CAMARA_EXPOSURE_MANUAL="1"
$env:CAMARA_EXPOSURE_VALOR="-6"

# Ajustar kernel de cierre morfológico para bandas LED
$env:HSV_KERNEL_OPEN="9"
$env:HSV_KERNEL_CLOSE="17"

# Opcional: Activar aceleración por GPU si existe tarjeta NVIDIA con CUDA
$env:YOLO_DEVICE="cuda"
```

---

## 📡 Endpoints HTTP Principales (Puerto 5001)

- `GET /datos-huevo`: Retorna el JSON con `largo`, `ancho`, `peso`, `elipsoide`, `revolucion`, `bascula`, `volumen_real`, `categoria` y `metodo_deteccion`.
- `GET /frame.jpg`: Transmisión en tiempo real en formato JPEG.
- `GET /dispositivos-disponibles`: Lista de puertos COM seriales y cámaras conectadas.
- `POST /guardar-configuracion`: Actualiza y persiste el puerto de báscula y cámara en `config.json`.
- `POST /desconectar-hardware`: Libera la cámara y cierra el puerto serie cleanly.
