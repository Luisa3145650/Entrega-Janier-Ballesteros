using System.Configuration;
using System.Threading.Tasks;
using System.Data;
using System.Windows;

namespace loginavicola
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public App()
        {
            AppContext.SetSwitch("System.Data.Sqlite.UseSqlitePCLRaw", true);
        }
    }



}
