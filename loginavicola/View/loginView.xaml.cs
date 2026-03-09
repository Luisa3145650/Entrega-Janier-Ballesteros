using loginavicola;
using loginavicola.Model;  // Sin tilde para coincidir con el espacio de nombres real
using loginavícola.Model;
using loginavicola.View;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace loginavicola.View
{
    /// <summary>
    /// Lógica de interacción para loginView.xaml
    /// </summary>
    public partial class loginView : Window
    {
        private DatabaseModel dbModel;

        public loginView()
        {
            InitializeComponent();
            dbModel = new DatabaseModel();

            // INICIALIZAR LA BASE DE DATOS
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

            // Validar campos vacíos
            if (string.IsNullOrEmpty(usuario) || string.IsNullOrEmpty(clave))
            {
                MessageBox.Show("Por favor, ingresa usuario y contraseña",
                                "Campos vacíos",
                                MessageBoxButton.OK,
                                MessageBoxImage.Warning);
                return;
            }

            try
            {
                // CONSULTA A LA BASE DE DATOS
                string query = "SELECT * FROM usuarios WHERE username = @username AND password = @password";

                SQLiteParameter[] parameters = new SQLiteParameter[]
                {
                    new SQLiteParameter("@username", usuario),   // Corregido: usuario en lugar de usuarioIngresado
                    new SQLiteParameter("@password", clave)      // Corregido: clave en lugar de claveIngresada
                };

                DataTable result = dbModel.ExecuteQuery(query, parameters);

                if (result.Rows.Count > 0)
                {
                    // Login exitoso
                    string nombreUsuario = result.Rows[0]["nombre"].ToString();
                    string rol = result.Rows[0]["rol"].ToString();

                    MessageBox.Show($"¡Bienvenido {nombreUsuario}!",
                                    "Login exitoso",
                                    MessageBoxButton.OK,
                                    MessageBoxImage.Information);

                    // Navegar a la ventana principal
                    MainWindow main = new MainWindow();
                    main.Show();
                    this.Close();
                }
                else
                {
                    MessageBox.Show("Usuario o contraseña incorrectos",
                                    "Error de login",
                                    MessageBoxButton.OK,
                                    MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al conectar con la base de datos: {ex.Message}",
                                "Error",
                                MessageBoxButton.OK,
                                MessageBoxImage.Error);
            }
        }

        private void Btnregistro_Click(object sender, RoutedEventArgs e)
        {
            Registrarse registroWindow = new Registrarse();
            registroWindow.ShowDialog();
        }
    }
}