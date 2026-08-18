using System;
using System.Data.SQLite;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;

namespace loginavicola.Database
{
    public static class DatabaseHelper
    {
        // Ruta legada en bin/
        private static readonly string legacyDbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "sistema_avicola.db");

        // Ruta nueva centralizada en %APPDATA%\LoginAvicola\
        private static readonly string appDataFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "LoginAvicola");

        public static readonly string DbPath = Path.Combine(appDataFolder, "sistema_avicola.db");
        public static readonly string BackupFolder = Path.Combine(appDataFolder, "Backups");
        public static readonly string ConnectionString = $"Data Source={DbPath};Version=3;";

        private static bool _inicializado = false;

        /// <summary>
        /// Inicialización explícita de la base de datos y migración inicial desde bin/ hacia %APPDATA%.
        /// Debe llamarse una sola vez desde App.xaml.cs al arrancar la aplicación.
        /// </summary>
        public static void Inicializar()
        {
            if (_inicializado) return;

            try
            {
                if (!Directory.Exists(appDataFolder))
                {
                    Directory.CreateDirectory(appDataFolder);
                }

                if (!Directory.Exists(BackupFolder))
                {
                    Directory.CreateDirectory(BackupFolder);
                }

                // Migración segura si no existe en %APPDATA% pero sí en bin/
                if (!File.Exists(DbPath) && File.Exists(legacyDbPath))
                {
                    File.Copy(legacyDbPath, DbPath, overwrite: false);
                    Debug.WriteLine($"[Migración BD] Archivo .db copiado desde '{legacyDbPath}' hacia '{DbPath}'.");
                }

                _inicializado = true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Error Inicializar BD] {ex.Message}");
            }
        }

        /// <summary>
        /// Genera un respaldo online usando la API nativa SQLiteConnection.BackupDatabase().
        /// </summary>
        public static bool GenerarRespaldo(bool esManual = false)
        {
            try
            {
                Inicializar();

                if (!File.Exists(DbPath)) return false;

                string fechaHoy = DateTime.Now.ToString("yyyy-MM-dd");
                string nombreBackup = $"sistema_avicola_{fechaHoy}.db";
                string rutaBackupHoy = Path.Combine(BackupFolder, nombreBackup);

                // Evitar respaldos automáticos duplicados en el mismo día
                if (!esManual && File.Exists(rutaBackupHoy))
                {
                    return true;
                }

                // Uso de la API nativa de respaldo de SQLite (Online Backup API)
                using (var origenConn = new SQLiteConnection(ConnectionString))
                using (var destinoConn = new SQLiteConnection($"Data Source={rutaBackupHoy};Version=3;"))
                {
                    origenConn.Open();
                    destinoConn.Open();
                    origenConn.BackupDatabase(destinoConn, "main", "main", -1, null, 0);
                }

                Debug.WriteLine($"[Backup Nativo SQLite] Respaldo generado con éxito: {rutaBackupHoy}");

                LimpiarRespaldosAntiguos();
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Error Backup Nativo] {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Mantiene únicamente los últimos 7 respaldos parseando la fecha del nombre del archivo.
        /// </summary>
        private static void LimpiarRespaldosAntiguos()
        {
            try
            {
                if (!Directory.Exists(BackupFolder)) return;

                var respaldos = new DirectoryInfo(BackupFolder)
                    .GetFiles("sistema_avicola_*.db")
                    .Select(archivo =>
                    {
                        string nombreSinExt = Path.GetFileNameWithoutExtension(archivo.Name);
                        string fechaStr = nombreSinExt.Replace("sistema_avicola_", "");

                        bool parseOk = DateTime.TryParseExact(
                            fechaStr, 
                            "yyyy-MM-dd", 
                            CultureInfo.InvariantCulture, 
                            DateTimeStyles.None, 
                            out DateTime fechaParsed);

                        return new { Archivo = archivo, Fecha = parseOk ? fechaParsed : DateTime.MinValue };
                    })
                    .OrderByDescending(x => x.Fecha)
                    .ToList();

                if (respaldos.Count > 7)
                {
                    var paraEliminar = respaldos.Skip(7);
                    foreach (var item in paraEliminar)
                    {
                        item.Archivo.Delete();
                        Debug.WriteLine($"[Retención] Borrado respaldo antiguo: {item.Archivo.Name}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Error Retención] {ex.Message}");
            }
        }
    }
}
