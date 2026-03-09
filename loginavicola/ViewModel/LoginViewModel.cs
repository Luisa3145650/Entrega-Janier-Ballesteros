using System;
using System.Windows;
using System.Windows.Input;
using loginavicola.Model;
// Agregamos esta referencia si tus comandos están en otra carpeta
// using loginavicola.ViewModel; 

namespace loginavicola.ViewModel
{
    // ERROR 1: ViewModelBase debe existir. Si no la tienes, hereda de INotifyPropertyChanged o créala.
    public class LoginViewModel : ViewModelBase
    {
        public string Username { get; set; }
        public string Password { get; set; }

        public ICommand LoginCommand { get; }

        public LoginViewModel()
        {
            // ERROR 2: ViewModelCommand debe estar definido abajo o en un archivo aparte
            LoginCommand = new ViewModelCommand(ExecuteLoginCommand);
        }

        private void ExecuteLoginCommand(object obj)
        {
            var usuario = ValidarUsuario(Username, Password);

            if (usuario != null)
            {
                // ERROR 4 y 5: Asegúrate de que la clase UserSession exista (la creamos abajo)
                UserSession.UsuarioActual = usuario;

                var mainWin = new MainWindow();
                mainWin.Show();

                // Cerramos la ventana de Login
                foreach (Window item in Application.Current.Windows)
                {
                    if (item is loginavicola.View.loginView) // Ajusta al nombre real de tu ventana login
                        item.Close();
                }
            }
            else
            {
                MessageBox.Show("Usuario o contraseña incorrectos");
            }
        }

        private Usuario ValidarUsuario(string user, string pass)
        {
            // ERROR 3: 'userFromDb' no existía. Debes retornar null o el objeto buscado.
            Usuario usuarioEncontrado = null;

            // Aquí iría tu lógica de base de datos...

            return usuarioEncontrado;
        }
    }

    // --- CLASES FALTANTES QUE CAUSAN TUS ERRORES ---

    // Solución al error de ViewModelCommand
    public class ViewModelCommand : ICommand
    {
        private readonly Action<object> _execute;
        public ViewModelCommand(Action<object> execute) => _execute = execute;
        public bool CanExecute(object parameter) => true;
        public void Execute(object parameter) => _execute(parameter);
        public event EventHandler CanExecuteChanged { add { } remove { } }
    }

    // Solución al error de UserSession
    public static class UserSession
    {
        public static Usuario UsuarioActual { get; set; }
    }
}