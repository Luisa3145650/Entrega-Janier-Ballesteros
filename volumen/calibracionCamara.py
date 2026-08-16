"""
Script de calibracion - correlo UNA SOLA VEZ (o cada vez que muevas la camara/brazo)
para obtener el valor real de PIXELES_POR_CM a 640x480.

COMO USARLO:
1. Cierra la app WPF y el servidor_api.py si estan corriendo (para liberar la camara).
2. Pon sobre la bandeja, EXACTAMENTE a los mismos 8 cm de distancia de la camara
   donde luego va el huevo, un objeto de medida conocida y de forma ovalada/circular
   claramente distinta al fondo. Lo mas facil: una moneda (mide su diametro con una
   regla, ej. 2.3 cm) o un huevo real que ya hayas medido con un calibrador/regla.
   El script ya esta configurado para usar la camara "HD Pro Webcam C920"
   automaticamente (variable NOMBRE_CAMARA), no la webcam integrada del laptop.
3. Corre: python calibrar_camara.py
4. Se abrira una ventana con el video en vivo y la mascara de deteccion.
   - Si el objeto no aparece en la mascara (queda todo negro), ajusta la iluminacion
     o el rango de color mas abajo (RANGO_HSV_1 / RANGO_HSV_2).
5. Cuando el objeto se detecte bien (elipse amarilla dibujada sobre el objeto),
   presiona la tecla ESPACIO. La consola imprimira el eje mayor y menor en pixeles.
6. Divide el eje mayor en pixeles entre la medida REAL en cm que mediste con la regla.
   Ese resultado es tu nuevo PIXELES_POR_CM. Cópialo a servidor_api.py.

Ejemplo: si el objeto mide 5.5 cm de largo real y en pantalla salio "eje_mayor=150px":
    PIXELES_POR_CM = 150 / 5.5 = 27.27

Presiona 'q' para salir sin calibrar.
"""

import cv2
import numpy as np
import json
import os

# Mismo rango de color que usa servidor_api.py - ajustalo si tu objeto no se detecta
RANGO_HSV_1 = (np.array([0, 50, 90]), np.array([30, 255, 255]))
RANGO_HSV_2 = (np.array([0, 0, 200]), np.array([180, 30, 255]))

# El emparejamiento de camara por NOMBRE (via pygrabber) puede fallar porque el
# orden que reporta pygrabber no siempre coincide con el indice real que usa
# OpenCV/DirectShow, sobre todo en laptops con camara integrada. Por eso aqui
# usamos seleccion MANUAL VISUAL: te muestro cada camara para que confirmes cual
# es la C920 con tus propios ojos, y ese indice se guarda en este mismo archivo
# (indice_camara.json, al lado del script) para no repetir el proceso cada vez.
ARCHIVO_INDICE_GUARDADO = os.path.join(os.path.dirname(os.path.abspath(__file__)), "indice_camara.json")
MAX_INDICES_A_PROBAR = 6


def cargar_indice_guardado():
    if os.path.exists(ARCHIVO_INDICE_GUARDADO):
        try:
            with open(ARCHIVO_INDICE_GUARDADO, "r") as f:
                return json.load(f).get("indice")
        except Exception:
            return None
    return None


def guardar_indice(indice):
    with open(ARCHIVO_INDICE_GUARDADO, "w") as f:
        json.dump({"indice": indice}, f)


def seleccionar_camara_manual():
    """Recorre los indices 0..MAX_INDICES_A_PROBAR mostrando video en vivo de cada
    uno, para que el usuario confirme visualmente cual es la C920."""
    print("\n=== SELECCION MANUAL DE CAMARA ===")
    print("Se abrira el video de cada camara disponible, una por una.")
    print("  y = SI, esta es la C920 (usar esta)")
    print("  n = NO, probar la siguiente")
    print("  q = cancelar todo\n")

    for indice in range(MAX_INDICES_A_PROBAR):
        cap = cv2.VideoCapture(indice, cv2.CAP_DSHOW)
        if not cap.isOpened():
            cap.release()
            continue

        cap.set(cv2.CAP_PROP_FRAME_WIDTH, 640)
        cap.set(cv2.CAP_PROP_FRAME_HEIGHT, 480)

        print(f"Mostrando camara indice {indice}. ¿Es la HD Pro Webcam C920? (y/n/q)")
        seleccionada = False
        cancelado = False

        while True:
            ret, frame = cap.read()
            if ret:
                cv2.putText(frame, f"Indice {indice} - y=usar esta / n=siguiente / q=salir",
                            (10, 25), cv2.FONT_HERSHEY_SIMPLEX, 0.55, (0, 255, 255), 2)
                cv2.imshow("¿Es esta la C920?", frame)

            tecla = cv2.waitKey(30) & 0xFF
            if tecla == ord('y'):
                seleccionada = True
                break
            elif tecla == ord('n'):
                break
            elif tecla == ord('q'):
                cancelado = True
                break

        cap.release()
        cv2.destroyWindow("¿Es esta la C920?")

        if seleccionada:
            print(f"Confirmado: usando indice {indice} como C920.")
            guardar_indice(indice)
            return indice
        if cancelado:
            return None

    print("No se encontro/confirmo ninguna camara en los indices probados.")
    return None


def main():
    indice_camara = cargar_indice_guardado()

    if indice_camara is not None:
        print(f"Usando indice guardado previamente: {indice_camara}")
        print("(si esta vez SI es la camara correcta, ignora esto y sigue calibrando)")
        print("Si quieres volver a elegir manualmente, borra el archivo indice_camara.json y corre de nuevo.\n")
    else:
        indice_camara = seleccionar_camara_manual()
        if indice_camara is None:
            print("Calibracion cancelada: no se confirmo ninguna camara.")
            return

    cap = cv2.VideoCapture(indice_camara, cv2.CAP_DSHOW)
    cap.set(cv2.CAP_PROP_FOURCC, cv2.VideoWriter_fourcc(*'MJPG'))
    cap.set(cv2.CAP_PROP_FRAME_WIDTH, 640)
    cap.set(cv2.CAP_PROP_FRAME_HEIGHT, 480)
    cap.set(cv2.CAP_PROP_BUFFERSIZE, 1)

    if not cap.isOpened():
        print("No se pudo abrir la camara. Revisa el INDICE_CAMARA o si otra app la esta usando.")
        return

    print("Ventana abierta. ESPACIO = capturar medida | q = salir")

    while True:
        ret, frame = cap.read()
        if not ret:
            continue

        hsv = cv2.cvtColor(cv2.GaussianBlur(frame, (5, 5), 0), cv2.COLOR_BGR2HSV)
        mascara = cv2.bitwise_or(
            cv2.inRange(hsv, *RANGO_HSV_1),
            cv2.inRange(hsv, *RANGO_HSV_2)
        )
        kernel = cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (9, 9))
        mascara = cv2.morphologyEx(cv2.morphologyEx(mascara, cv2.MORPH_OPEN, kernel, iterations=2), cv2.MORPH_CLOSE, kernel, iterations=3)

        contornos, _ = cv2.findContours(mascara, cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_SIMPLE)

        eje_mayor_actual, eje_menor_actual = None, None

        if contornos:
            cnt = max(contornos, key=cv2.contourArea)
            if cv2.contourArea(cnt) > 800 and len(cnt) >= 5:
                (x, y), (eje_menor, eje_mayor), angulo = cv2.fitEllipse(cnt)
                eje_mayor_actual, eje_menor_actual = eje_mayor, eje_menor
                cv2.ellipse(frame, ((x, y), (eje_menor, eje_mayor), angulo), (0, 255, 255), 2)
                cv2.putText(frame, f"mayor={eje_mayor:.1f}px menor={eje_menor:.1f}px",
                            (10, 30), cv2.FONT_HERSHEY_SIMPLEX, 0.6, (0, 255, 255), 2)

        cv2.imshow("Video (ESPACIO=capturar, q=salir)", frame)
        cv2.imshow("Mascara de deteccion", mascara)

        tecla = cv2.waitKey(1) & 0xFF
        if tecla == ord('q'):
            break
        elif tecla == ord(' ') and eje_mayor_actual is not None:
            print("\n=== MEDIDA CAPTURADA ===")
            print(f"Eje mayor: {eje_mayor_actual:.1f} px")
            print(f"Eje menor: {eje_menor_actual:.1f} px")
            print("Ahora divide el eje mayor (o menor) entre la medida real en cm de tu objeto.")
            print("Ese resultado es tu PIXELES_POR_CM. Pegalo en servidor_api.py.\n")

    cap.release()
    cv2.destroyAllWindows()


if __name__ == "__main__":
    main()