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
                // 🔥 AQUÍ USAMOS TU MÉTODO REAL
                Usuario usuarioLogeado = db.ValidarLogin(usuario, clave);

                if (usuarioLogeado != null)
                {
                    UserSession.UsuarioActual = usuarioLogeado;
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


    }
}