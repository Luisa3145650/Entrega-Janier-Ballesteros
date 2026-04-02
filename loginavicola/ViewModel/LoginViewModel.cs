using System;
using System.Windows;
using System.Windows.Input;
using loginavicola.Model;
using loginavicola.Database;

namespace loginavicola.ViewModel
{
    public class LoginViewModel : ViewModelBase
    {
        private readonly UsuarioDatabase database = new UsuarioDatabase();

        public string Username { get; set; }
        public string Password { get; set; }

        public ICommand LoginCommand { get; }

        public LoginViewModel()
        {
            LoginCommand = new ViewModelCommand(ExecuteLoginCommand);
        }

        private void ExecuteLoginCommand(object obj)
        {
            // Usamos la función que ya existe en tu UsuarioDatabase
            // Ella se encarga de encriptar la clave y buscar al usuario
            var usuario = database.ValidarLogin(Username, Password);

            if (usuario != null)
            {
                UserSession.UsuarioActual = usuario;

                var mainWin = new MainWindow();
                mainWin.Show();

                // Cerramos la ventana de Login
                foreach (Window item in Application.Current.Windows)
                {
                    if (item.DataContext == this)
                        item.Close();
                }
            }
            else
            {
                MessageBox.Show("Usuario o contraseña incorrectos");
            }
        }

        // Ya no necesitamos el método ValidarUsuario aquí porque 
        // usamos directamente database.ValidarLogin arriba.
    }

    public class ViewModelCommand : ICommand
    {
        private readonly Action<object> _execute;
        public ViewModelCommand(Action<object> execute) => _execute = execute;
        public bool CanExecute(object parameter) => true;
        public void Execute(object parameter) => _execute(parameter);
        public event EventHandler CanExecuteChanged { add { } remove { } }
    }

    public static class UserSession
    {
        public static Usuario UsuarioActual { get; set; }
    }
}