using System;
using System.Windows;
using System.Windows.Input;

namespace loginavicola.View
{
    public partial class Registrarse : Window
    {
        public Registrarse()
        {
            InitializeComponent();

            // Permitir arrastrar la ventana
            this.MouseDown += Window_MouseDown;
        }

        // Permitir mover la ventana
        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                this.DragMove();
        }

        
        private void btnMinimize_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }

        
        private void btnCloseRegistro_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
        private void btnVolver_Click(object sender, RoutedEventArgs e)
        {
            loginView ventanaLogin = new loginView();
            ventanaLogin.Show();  // Mostrar la ventana login
            this.Close();         // Cerrar la actual
        }


        private void BtnRegistrar_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                
                if (string.IsNullOrWhiteSpace(txtUser.Text))
                {
                    MessageBox.Show("Por favor ingrese un nombre de usuario",
                                  "Campo requerido",
                                  MessageBoxButton.OK,
                                  MessageBoxImage.Warning);
                    txtUser.Focus();
                    return;
                }

                if (string.IsNullOrWhiteSpace(txtPassword.Password))
                {
                    MessageBox.Show("Por favor ingrese una contraseña",
                                  "Campo requerido",
                                  MessageBoxButton.OK,
                                  MessageBoxImage.Warning);
                    txtPassword.Focus();
                    return;
                }

                if (string.IsNullOrWhiteSpace(txtPasswords.Password))
                {
                    MessageBox.Show("Por favor confirme su contraseña",
                                  "Campo requerido",
                                  MessageBoxButton.OK,
                                  MessageBoxImage.Warning);
                    txtPasswords.Focus();
                    return;
                }

                
                if (txtPassword.Password != txtPasswords.Password)
                {
                    MessageBox.Show("Las contraseñas no coinciden",
                                  "Error de validación",
                                  MessageBoxButton.OK,
                                  MessageBoxImage.Warning);
                    txtPasswords.Focus();
                    txtPasswords.Clear();
                    return;
                }

                
                if (txtPassword.Password.Length < 6)
                {
                    MessageBox.Show("La contraseña debe tener al menos 6 caracteres",
                                  "Contraseña débil",
                                  MessageBoxButton.OK,
                                  MessageBoxImage.Warning);
                    txtPassword.Focus();
                    return;
                }

                
                bool registroExitoso = RegistrarUsuario(txtUser.Text, txtPassword.Password);

                if (registroExitoso)
                {
                    MessageBox.Show("Usuario registrado exitosamente",
                                  "Registro exitoso",
                                  MessageBoxButton.OK,
                                  MessageBoxImage.Information);

                    
                    LimpiarCampos();

                    
                }
                else
                {
                    MessageBox.Show("El usuario ya existe o hubo un error en el registro",
                                  "Error",
                                  MessageBoxButton.OK,
                                  MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al registrar usuario: {ex.Message}",
                              "Error",
                              MessageBoxButton.OK,
                              MessageBoxImage.Error);
            }
        }

        
        private bool RegistrarUsuario(string usuario, string contraseña)
        {
           

            return true;
        }

        
        private void LimpiarCampos()
        {
            txtUser.Clear();
            txtPassword.Clear();
            txtPasswords.Clear();
            txtUser.Focus();
        }
    }
}