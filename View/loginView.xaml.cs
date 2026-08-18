using loginavicola.Database;
using loginavicola.Model;
using loginavicola.View;
using System;
using System.Windows;
using System.Windows.Input;

namespace loginavicola.View
{
    public partial class loginView : Window
    {
        private UsuarioDatabase db;
        private bool _iniciandoSesionExitosamente = false;
        private bool _isShuttingDown = false;

        public loginView()
        {
            InitializeComponent();
            db = new UsuarioDatabase();
            this.Closing += Window_Closing;
        }

        private async void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (_iniciandoSesionExitosamente || _isShuttingDown)
            {
                return;
            }

            e.Cancel = true;
            _isShuttingDown = true;

            try
            {
                await Helpers.PythonProcessManager.DetenerAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error durante el apagado en loginView: {ex.Message}");
            }
            finally
            {
                Application.Current.Shutdown();
            }
        }

        private void Window_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
                DragMove();
        }

        private void btnMinimize_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void btnClose_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void BtnLogin_Click(object sender, RoutedEventArgs e)
        {
            string usuario = txtUser.Text.Trim();
            string clave = txtPassword.Password.Trim();

            if (string.IsNullOrEmpty(usuario) || string.IsNullOrEmpty(clave))
            {
                MessageBox.Show("Por favor, ingresa usuario y contraseña");
                return;
            }

            try
            {
                Usuario usuarioLogeado = db.ValidarLogin(usuario, clave);

                if (usuarioLogeado != null)
                {
                    UserSession.UsuarioActual = usuarioLogeado;
                    UserSession.EsVisitante = false;
                    MessageBox.Show($"¡Bienvenido {usuarioLogeado.Nombres}!");

                    _iniciandoSesionExitosamente = true;
                    MainWindow main = new MainWindow();
                    main.Show();
                    this.Close();
                }
                else
                {
                    MessageBox.Show("Usuario o contraseña incorrectos");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}");
            }
        }

        private void BtnVisitante_Click(object sender, RoutedEventArgs e)
        {
            // Crear un usuario visitante virtual
            UserSession.UsuarioActual = new Usuario
            {
                IdUsuario = 0,
                Nombres = "Visitante",
                Username = "visitante",
                Rol = "Visitante",
                PermisoLotes = false,
                PermisoProduccion = true,  // Solo producción visible
                PermisoAlimentacion = false,
                PermisoDiagnostico = false,
                PermisoInventario = false
            };
            UserSession.EsVisitante = true;

            MessageBox.Show("Bienvenido como visitante. Solo podrás ver el Dashboard y Producción.");

            _iniciandoSesionExitosamente = true;
            MainWindow main = new MainWindow();
            main.Show();
            this.Close();
        }
    }
}