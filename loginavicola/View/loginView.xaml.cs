using loginavicola;
using loginavicola.Model;
using loginavícola.Model;
using loginavicola.View;
using System;
using System.Data;
using System.Data.SQLite;
using System.Windows;
using System.Windows.Input;

namespace loginavicola.View
{
    public partial class loginView : Window
    {
        private DatabaseModel dbModel;

        public loginView()
        {
            InitializeComponent();
            dbModel = new DatabaseModel();
            dbModel.InicializarBaseDeDatos();
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
            string usuario = txtUser.Text;
            string clave = txtPassword.Password;

            if (string.IsNullOrEmpty(usuario) || string.IsNullOrEmpty(clave))
            {
                MessageBox.Show("Por favor, ingresa usuario y contraseña", "Campos vacíos", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                // --- EL CAMBIO ESTÁ AQUÍ ---
                // Encriptamos la clave que escribió el usuario para que coincida con la de la DB
                string claveEncriptada = dbModel.EncriptarSHA256(clave);

                string query = "SELECT * FROM usuarios WHERE username = @username AND password = @password";

                SQLiteParameter[] parameters = new SQLiteParameter[]
                {
                    new SQLiteParameter("@username", usuario),
                    new SQLiteParameter("@password", claveEncriptada) // Enviamos la encriptada
                };

                DataTable result = dbModel.ExecuteQuery(query, parameters);

                if (result.Rows.Count > 0)
                {
                    string nombreUsuario = result.Rows[0]["nombre"].ToString();
                    MessageBox.Show($"¡Bienvenido {nombreUsuario}!", "Login exitoso", MessageBoxButton.OK, MessageBoxImage.Information);

                    MainWindow main = new MainWindow();
                    main.Show();
                    this.Close();
                }
                else
                {
                    MessageBox.Show("Usuario o contraseña incorrectos", "Error de login", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al conectar con la base de datos: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Btnregistro_Click(object sender, RoutedEventArgs e)
        {
            Registrarse registroWindow = new Registrarse();
            registroWindow.ShowDialog();
        }
    }
}