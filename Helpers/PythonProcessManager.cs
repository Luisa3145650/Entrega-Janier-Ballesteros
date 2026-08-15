using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace loginavicola.Helpers
{
    public static class PythonProcessManager
    {
        private static Process procesoPython;
        private static readonly HttpClient client = new HttpClient();
        private const string BASE_URL = "http://localhost:5001";

        // Rutas absolutas confirmadas: proyectoformativo\volumen (al lado de loginavicola\)
        private static readonly string RutaVenvPython =
            @"C:\Users\ferna\OneDrive\Documentos\proyectoformativo\volumen\venv\Scripts\python.exe";
        private static readonly string RutaScript =
            @"C:\Users\ferna\OneDrive\Documentos\proyectoformativo\volumen\servidor_api.py";

        /// <summary>
        /// Revisa directamente el config.json compartido con Python (en ProgramData) para
        /// saber si este equipo ya tiene puerto de bascula y camara configurados.
        /// Se usa en App.xaml.cs para decidir si mostrar ConfiguracionHardwareWindow.
        /// </summary>
        public static bool EstaConfigurado()
        {
            try
            {
                string rutaConfig = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                    "ClasificadorHuevos", "config.json");

                if (!File.Exists(rutaConfig)) return false;

                string json = File.ReadAllText(rutaConfig);
                using var doc = System.Text.Json.JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("configurado", out var prop))
                    return prop.GetBoolean();

                return false;
            }
            catch
            {
                return false;
            }
        }

        public static void Iniciar()
        {
            try
            {
                if (!File.Exists(RutaVenvPython))
                {
                    Debug.WriteLine($"⚠️ No se encontró python.exe en: {RutaVenvPython}");
                    return;
                }
                if (!File.Exists(RutaScript))
                {
                    Debug.WriteLine($"⚠️ No se encontró servidor_api.py en: {RutaScript}");
                    return;
                }
                // Si por alguna razón ya hay un proceso corriendo (ej. reinicio en caliente), no lo dupliques
                if (procesoPython != null && !procesoPython.HasExited)
                {
                    Debug.WriteLine("ℹ️ servidor_api.py ya está corriendo.");
                    return;
                }

                var psi = new ProcessStartInfo
                {
                    FileName = RutaVenvPython,
                    Arguments = $"\"{RutaScript}\"",
                    WorkingDirectory = Path.GetDirectoryName(RutaScript),
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                procesoPython = new Process { StartInfo = psi, EnableRaisingEvents = true };
                procesoPython.OutputDataReceived += (s, e) => { if (e.Data != null) Debug.WriteLine("[PY] " + e.Data); };
                procesoPython.ErrorDataReceived += (s, e) => { if (e.Data != null) Debug.WriteLine("[PY-ERR] " + e.Data); };
                procesoPython.Start();
                procesoPython.BeginOutputReadLine();
                procesoPython.BeginErrorReadLine();
                Debug.WriteLine("✅ servidor_api.py lanzado correctamente.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ Error iniciando servidor_api.py: {ex.Message}");
            }
        }

        /// <summary>
        /// Espera (con reintentos cortos) a que la API de Python ya esté respondiendo,
        /// para no consultar /dispositivos-disponibles o /estado-configuracion demasiado pronto
        /// justo después de Iniciar().
        /// </summary>
        public static async Task<bool> EsperarApiListaAsync(int intentos = 20, int esperaMs = 300)
        {
            for (int i = 0; i < intentos; i++)
            {
                try
                {
                    var respuesta = await client.GetAsync($"{BASE_URL}/estado-configuracion");
                    if (respuesta.IsSuccessStatusCode) return true;
                }
                catch
                {
                    // aun no está lista, seguimos intentando
                }
                await Task.Delay(esperaMs);
            }
            Debug.WriteLine("⚠️ La API de Python no respondió a tiempo.");
            return false;
        }

        /// <summary>
        /// APAGADO ORDENADO: le pide a Python que libere cámara y báscula (endpoint /shutdown)
        /// antes de morir. Solo si no responde en el tiempo dado, se recurre a Kill() como
        /// último recurso. Esto es lo que evita que los puertos queden bloqueados.
        /// SIEMPRE usa este método al cerrar la app, nunca llames Kill() directamente.
        /// </summary>
        public static async Task DetenerAsync()
        {
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                await client.PostAsync($"{BASE_URL}/shutdown", null, cts.Token);
                Debug.WriteLine("🛑 Solicitud de apagado ordenado enviada a servidor_api.py.");

                // le damos un momento a que realmente suelte cámara/puerto antes de continuar
                await Task.Delay(500);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"⚠️ No se pudo apagar ordenadamente ({ex.Message}), se forzará el cierre.");
            }
            finally
            {
                try
                {
                    if (procesoPython != null && !procesoPython.HasExited)
                    {
                        procesoPython.Kill(entireProcessTree: true);
                        Debug.WriteLine("🛑 servidor_api.py detenido por la fuerza (respaldo).");
                    }
                    procesoPython?.Dispose();
                }
                catch
                {
                    // el proceso ya pudo haberse cerrado solo
                }
            }
        }
    }
}