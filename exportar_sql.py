import os
import sqlite3

def exportar_base_datos():
    # 1. Obtener la ruta de %APPDATA%\LoginAvicola\sistema_avicola.db automáticamente
    db_path = os.path.expandvars(r'%APPDATA%\LoginAvicola\sistema_avicola.db')
    
    # 2. Definir la ruta del archivo .sql de salida en el mismo directorio del script
    output_filename = 'respaldo_datagrip.sql'
    script_dir = os.path.dirname(os.path.abspath(__file__))
    output_path = os.path.join(script_dir, output_filename)
    
    if not os.path.exists(db_path):
        print(f"[-] Error: No se encontró el archivo de base de datos en '{db_path}'")
        return

    print(f"[+] Conectando a la base de datos: {db_path}")

    # 3. Utilizar iterdump() para volcar esquema y datos (INSERTs)
    try:
        conn = sqlite3.connect(db_path)
        with open(output_path, 'w', encoding='utf-8') as f:
            for line in conn.iterdump():
                f.write(f'{line}\n')
        conn.close()
        
        print(f"[+] ¡Volcado completado con éxito!")
        print(f"[+] Archivo SQL generado en: {output_path}")
    except Exception as e:
        print(f"[-] Error durante la exportación: {e}")

if __name__ == '__main__':
    exportar_base_datos()
