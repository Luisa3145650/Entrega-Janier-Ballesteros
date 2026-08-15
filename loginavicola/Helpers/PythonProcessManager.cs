using System;
using System.Diagnostics;
using System.IO;

namespace loginavicola.Helpers
{
    public static class PythonProcessManager
    {
        private static Process procesoPython;

        // Rutas absolutas confirmadas: proyectoformativo\volumen (al lado de loginavicola\)
        private static readonly string RutaVenvPython =
            @"C:\Users\ferna\OneDrive\Documentos\proyectoformativo\volumen\venv\Scripts\python.exe";

        private static readonly string RutaScript =
            @"C:\Users\ferna\OneDrive\Documentos\proyectoformativo\volumen\servidor_api.py";

        public static void Iniciar()
        {
            try
            {
                if (!File.Exists(RutaVenvPython))
                {
                    Console.WriteLine($"⚠️ No se encontró python.exe en: {RutaVenvPython}");
                    return;
                }

                if (!File.Exists(RutaScript))
                {
                    Console.WriteLine($"⚠️ No se encontró servidor_api.py en: {RutaScript}");
                    return;
                }

                // Si por alguna razón ya hay un proceso corriendo (ej. reinicio en caliente), no lo dupliques
                if (procesoPython != null && !procesoPython.HasExited)
                {
                    Console.WriteLine("ℹ️ servidor_api.py ya está corriendo.");
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
                procesoPython.OutputDataReceived += (s, e) => { if (e.Data != null) Console.WriteLine("[PY] " + e.Data); };
                procesoPython.ErrorDataReceived += (s, e) => { if (e.Data != null) Console.WriteLine("[PY-ERR] " + e.Data); };

                procesoPython.Start();
                procesoPython.BeginOutputReadLine();
                procesoPython.BeginErrorReadLine();

                Console.WriteLine("✅ servidor_api.py lanzado correctamente.");
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
                if (procesoPython != null && !procesoPython.HasExited)
                {
                    procesoPython.Kill(entireProcessTree: true);
                    procesoPython.Dispose();
                    Console.WriteLine("🛑 servidor_api.py detenido.");
                }
            }
            catch
            {
                // el proceso ya pudo haberse cerrado solo
            }
        }
    }
}