# ⚠️ Módulo Deprecado / Archivatorio: modelo copia

> **⚠️ Este servidor FastAPI ya no se ejecuta en producción. La lógica de detección YOLOv8-seg fue migrada a volumen/servidor_api.py. Se conserva solo como referencia histórica.**

---

## 🚫 ADVERTENCIA DE EJECUCIÓN

**NO EJECUTAR `server.py` DENTRO DE ESTA CARPETA.**

### Razones Técnicas:
1. **Conflicto de Dispositivos de Hardware:** Ejecutar este servidor en paralelo con `volumen/servidor_api.py` generará bloqueos y contención en la cámara web por llamadas simultáneas a DirectShow (`VideoCapture(0)`).
2. **Duplicidad de Puertos:** Toda la información procesada por la red neuronal YOLOv8 ya es entregada directamente a la aplicación WPF de C# a través del servidor activo en `volumen/servidor_api.py` (puerto 5001).

---

## 📂 Contenido Archivatorio
- `modelo/best.pt`: Verificado contra `volumen/modelo/best.pt`. **Hash SHA256 idéntico:** `17A10D33F2780AB833B9124D488A62973D4D9CD1D7CE0731F94A1149A6E305B0` (6,787,444 bytes).
- `server.py`: Servidor FastAPI previo usado en la fase de prototipado inicial.

## ✅ Estado del Respaldo
Auditoría end-to-end confirmada: la integración YOLOv8-seg está 100% activa en el entorno virtual de producción utilizado por C# (`volumen/venv`). `modelo copia/` ya no es necesario como respaldo activo, pero se conserva archivado a discreción del usuario.
