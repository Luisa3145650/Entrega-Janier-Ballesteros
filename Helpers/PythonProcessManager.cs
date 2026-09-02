using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace loginavicola.Helpers
{
    public static class PythonProcessManager
    {
        private static Process procesoPython;
        private static readonly HttpClient httpClient = new HttpClient { Timeout = TimeSpan.FromMilliseconds(700) };

        public static void Iniciar()
        {
            try
            {
                // Buscar script servidor_api.py en posibles rutas
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string[] rutasScript = new[]
                {
                    Path.Combine(baseDir, "volumen", "servidor_api.py"),
                    Path.Combine(Directory.GetCurrentDirectory(), "volumen", "servidor_api.py"),
                    @"C:\Users\ferna\OneDrive\Documentos\Janier-repositorio\Janier-repositorio\loginavicola\volumen\servidor_api.py",
                    @"C:\Users\ferna\OneDrive\Documentos\proyectoformativo\volumen\servidor_api.py"
                };

                string rutaScriptEncontrada = null;
                foreach (var r in rutasScript)
                {
                    if (File.Exists(r))
                    {
                        rutaScriptEncontrada = r;
                        break;
                    }
                }

                if (rutaScriptEncontrada == null)
                {
                    Console.WriteLine("⚠️ No se encontró servidor_api.py.");
                    return;
                }

                string scriptDir = Path.GetDirectoryName(rutaScriptEncontrada);

                // Buscar intérprete python.exe
                string[] rutasPython = new[]
                {
                    Path.Combine(scriptDir, "venv", "Scripts", "python.exe"),
                    Path.Combine(baseDir, "venv", "Scripts", "python.exe"),
                    @"C:\Users\ferna\OneDrive\Documentos\proyectoformativo\volumen\venv\Scripts\python.exe",
                    "python.exe"
                };

                string rutaPythonEncontrada = "python.exe";
                foreach (var py in rutasPython)
                {
                    if (File.Exists(py))
                    {
                        rutaPythonEncontrada = py;
                        break;
                    }
                }

                if (procesoPython != null && !procesoPython.HasExited)
                {
                    Console.WriteLine("ℹ️ servidor_api.py ya está corriendo.");
                    return;
                }

                var psi = new ProcessStartInfo
                {
                    FileName = rutaPythonEncontrada,
                    Arguments = $"\"{rutaScriptEncontrada}\"",
                    WorkingDirectory = scriptDir,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                procesoPython = new Process { StartInfo = psi, EnableRaisingEvents = true };
                procesoPython.OutputDataReceived += (s, e) => { if (e.Data != null) Console.WriteLine("[PY] " + e.Data); };
                procesoPython.ErrorDataReceived += (s, e) => { if (e.Data != null) Console.WriteLine("[PY-ERR] " + e.Data); };

                procesoPython.Start();
                procesoPython.BeginOutputReadLine();
                procesoPython.BeginErrorReadLine();

                Console.WriteLine($"✅ servidor_api.py lanzado correctamente desde {rutaScriptEncontrada}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error iniciando servidor_api.py: {ex.Message}");
            }
        }

        public static void Detener()
        {
            try
            {
                // 1. Notificar a Python para liberar hardware (cámaras, puerto COM)
                try
                {
                    var taskDesconectar = httpClient.PostAsync("http://localhost:5001/desconectar-hardware", null);
                    taskDesconectar.Wait(300);
                }
                catch { }

                try
                {
                    var taskShutdown = httpClient.PostAsync("http://localhost:5001/shutdown", null);
                    taskShutdown.Wait(300);
                }
                catch { }

                // 2. Terminar proceso hijo registrado
                if (procesoPython != null && !procesoPython.HasExited)
                {
                    procesoPython.Kill(entireProcessTree: true);
                    procesoPython.Dispose();
                    procesoPython = null;
                }

                // 3. Matar cualquier proceso residual zombi de python ejecutando servidor_api
                try
                {
                    var psiKill = new ProcessStartInfo
                    {
                        FileName = "taskkill",
                        Arguments = "/F /IM python.exe /T",
                        CreateNoWindow = true,
                        UseShellExecute = false
                    };
                    Process.Start(psiKill)?.WaitForExit(500);
                }
                catch { }

                Console.WriteLine("🛑 Hardware liberado y procesos de Python detenidos.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Aviso deteniendo Python: {ex.Message}");
            }
        }
    }
}