using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace loginavicola.Helpers
{
    public static class PythonProcessManager
    {
        private static Process procesoPython;
        private static readonly HttpClient client = new HttpClient();
        private const string BASE_URL = "http://localhost:5001";

        private static string ObtenerRutaScript()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;

            // 1. En la carpeta de ejecución (copiado por MSBuild desde loginavicola.csproj)
            string rutaLocal = Path.Combine(baseDir, "servidor_api.py");
            if (File.Exists(rutaLocal)) return rutaLocal;

            // 2. Fuente única de verdad: carpeta del proyecto loginavicola
            string rutaProyecto = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "servidor_api.py"));
            if (File.Exists(rutaProyecto)) return rutaProyecto;

            // 3. Carpeta raíz de la solución o carpeta volumen
            string rutaSolucion = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", "servidor_api.py"));
            if (File.Exists(rutaSolucion)) return rutaSolucion;

            string rutaVolumen = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "volumen", "servidor_api.py"));
            if (File.Exists(rutaVolumen)) return rutaVolumen;

            throw new FileNotFoundException(
                $"No se encontró el script del servidor Python (servidor_api.py).\n\nRutas buscadas:\n1. {rutaLocal}\n2. {rutaProyecto}\n3. {rutaSolucion}\n4. {rutaVolumen}");
        }

        private static string ObtenerRutaVenvPython()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;

            // 1. Entorno Python embebido en producción (relativo al binario ejecutable)
            string rutaEmbed = Path.Combine(baseDir, "python-embed", "python.exe");
            if (File.Exists(rutaEmbed)) return rutaEmbed;

            // 2. Entorno virtual venv de desarrollo (relativo al proyecto)
            string rutaVenvDev = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "volumen", "venv", "Scripts", "python.exe"));
            if (File.Exists(rutaVenvDev)) return rutaVenvDev;

            // 3. Entorno virtual alternativo relativo dentro de la estructura de proyecto
            string rutaVenvAlt = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "venv", "Scripts", "python.exe"));
            if (File.Exists(rutaVenvAlt)) return rutaVenvAlt;

            throw new FileNotFoundException(
                $"No se encontró el ejecutable de Python (python.exe).\n\nRutas buscadas:\n1. {rutaEmbed}\n2. {rutaVenvDev}\n3. {rutaVenvAlt}");
        }

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
                string rutaPython = ObtenerRutaVenvPython();
                string rutaScript = ObtenerRutaScript();

                if (!File.Exists(rutaPython))
                {
                    MessageBox.Show($"No se encontró el ejecutable de Python en:\n{rutaPython}",
                        "Error de Entorno Python", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                if (!File.Exists(rutaScript))
                {
                    MessageBox.Show($"No se encontró el script del servidor Python en:\n{rutaScript}",
                        "Error de Script Python", MessageBoxButton.OK, MessageBoxImage.Error);
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
                    FileName = rutaPython,
                    Arguments = $"\"{rutaScript}\"",
                    WorkingDirectory = Path.GetDirectoryName(rutaScript),
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
                Debug.WriteLine($"✅ servidor_api.py ({rutaScript}) lanzado correctamente.");
            }
            catch (FileNotFoundException fnfEx)
            {
                Debug.WriteLine($"❌ {fnfEx.Message}");
                MessageBox.Show(fnfEx.Message, "Error de Inicialización de Hardware", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ Error iniciando servidor_api.py: {ex.Message}");
                MessageBox.Show($"Error al iniciar el servidor de visión Python:\n{ex.Message}", "Error Crítico", MessageBoxButton.OK, MessageBoxImage.Error);
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
            if (procesoPython == null || procesoPython.HasExited)
            {
                Debug.WriteLine("ℹ️ No hay proceso Python activo para detener.");
                return;
            }

            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(4));
                var response = await client.PostAsync($"{BASE_URL}/shutdown", null, cts.Token);
                if (response.IsSuccessStatusCode)
                {
                    Debug.WriteLine("🛑 Solicitud de apagado ordenado enviada a servidor_api.py correctamente.");
                }
                else
                {
                    Debug.WriteLine($"⚠️ /shutdown respondió con estado {response.StatusCode}.");
                }

                // Tiempo de gracia para permitir la liberación de dispositivos y salida del proceso Python
                await Task.Delay(500);
            }
            
            catch (TaskCanceledException)
            {
                Debug.WriteLine("⚠️ Timeout (4s) esperando respuesta de /shutdown en servidor_api.py.");
            }
            catch (HttpRequestException ex)
            {
                Debug.WriteLine($"⚠️ Error de red al comunicarse con /shutdown: {ex.Message}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"⚠️ Excepción inesperada durante /shutdown: {ex.Message}");
            }
            finally
            {
                try
                {
                    if (procesoPython != null && !procesoPython.HasExited)
                    {
                        Debug.WriteLine("🛑 Forzando cierre del proceso Python con Kill() (respaldo de emergencia)...");
                        procesoPython.Kill(entireProcessTree: true);

                        // Breve espera de verificación de cierre
                        await Task.Delay(300);
                    }

                    procesoPython?.Dispose();
                    procesoPython = null;
                    Debug.WriteLine("✅ Objeto de proceso Python limpiado completamente.");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error al limpiar recurso de proceso Python: {ex.Message}");
                    procesoPython = null;
                }
            }
        }
    }
}