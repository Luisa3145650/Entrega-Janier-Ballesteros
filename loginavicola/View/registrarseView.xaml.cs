using System;
using System.Windows;
using System.Windows.Input;
using System.Data.SQLite;
using loginavícola.Model; // <--- AQUÍ ESTÁ LA TILDE

namespace loginavicola.View
{
    public partial class Registrarse : Window
    {
        // Usamos el namespace con tilde para crear la instancia
        private loginavícola.Model.DatabaseModel dbModel = new loginavícola.Model.DatabaseModel();

        public Registrarse()
        {
            InitializeComponent();
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed) DragMove();
        }

        private void btnMinimize_Click(object sender, RoutedEventArgs e) => this.WindowState = WindowState.Minimized;

        private void btnCloseRegistro_Click(object sender, RoutedEventArgs e) => this.Close();

        private void btnVolver_Click(object sender, RoutedEventArgs e)
        {
            loginView login = new loginView();
            login.Show();
            this.Close();
        }

        private void BtnRegistrar_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtUser.Text) || string.IsNullOrWhiteSpace(txtPassword.Password))
            {
                MessageBox.Show("Por favor, llena los campos.");
                return;
            }

            try
            {
                string passEnc = dbModel.EncriptarSHA256(txtPassword.Password);
                string query = "INSERT INTO usuarios (username, password, nombre, rol) VALUES (@u, @p, @n, 'user')";

                SQLiteParameter[] p = {
                    new SQLiteParameter("@u", txtUser.Text),
                    new SQLiteParameter("@p", passEnc),
                    new SQLiteParameter("@n", txtUser.Text)
                };

                if (dbModel.ExecuteNonQuery(query, p) > 0)
                {
                    MessageBox.Show("¡Usuario registrado con éxito!");
                    btnVolver_Click(null, null);
                }
            }
            catch (Exception) { MessageBox.Show("Error: El usuario ya existe."); }
        }
    }
}