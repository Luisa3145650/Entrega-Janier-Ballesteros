using System;
using System.Configuration;
using System.Data;
using System.Threading.Tasks;
using System.Windows;
using loginavicola.Helpers;
using loginavicola.View;

namespace loginavicola
{
    public partial class App : Application
    {
        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            try
            {
                // 0. Inicializar BD en %APPDATA% y generar respaldo automático inicial
                Database.DatabaseHelper.Inicializar();
                Database.DatabaseHelper.GenerarRespaldo(esManual: false);

                // 1. Iniciar el proceso en segundo plano de Python
                PythonProcessManager.Iniciar();

                // 2. Esperar a que Flask esté listo para responder peticiones
                await PythonProcessManager.EsperarApiListaAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"No se pudo conectar con el servidor de hardware (Python).\n\nDetalle: {ex.Message}",
                    "Error de Inicialización",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                // Aseguramos la limpieza del proceso si algo falló
                await PythonProcessManager.DetenerAsync();
                Shutdown();
                return;
            }

            // El hardware ahora se configura directamente en produccionView.xaml.cs sin bloquear el inicio.
        }

        protected override void OnExit(ExitEventArgs e)
        {
            try
            {
                // Forzar la espera sincrónica del endpoint /shutdown para liberar la cámara y puerto COM
                PythonProcessManager.DetenerAsync().GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error al apagar el servidor Python: {ex.Message}");
            }

            base.OnExit(e);
        }
    }
}