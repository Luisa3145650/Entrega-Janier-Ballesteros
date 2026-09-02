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

        public loginView()
        {
            InitializeComponent();
            db = new UsuarioDatabase();
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
            Application.Current.Shutdown();
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

            MainWindow main = new MainWindow();
            main.Show();
            this.Close();
        }
    }
}