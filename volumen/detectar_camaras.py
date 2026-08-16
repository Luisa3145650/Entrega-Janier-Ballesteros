from pygrabber.dshow_graph import FilterGraph

graph = FilterGraph()
dispositivos = graph.get_input_devices()

print("Camaras detectadas por DirectShow (indice -> nombre):\n")
for i, nombre in enumerate(dispositivos):
    print(f"  Indice {i}: {nombre}")

print("\nBusca el indice que diga 'HD Pro Webcam C920' -- ese es el que debes usar en cv2.VideoCapture(indice).")