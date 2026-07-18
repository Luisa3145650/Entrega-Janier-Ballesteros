using System.Configuration;
using System.Threading.Tasks;
using System.Data;
using System.Windows;

namespace loginavicola
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            loginavicola.Helpers.PythonProcessManager.Iniciar();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            loginavicola.Helpers.PythonProcessManager.Detener();
            base.OnExit(e);
        }
    }
}
