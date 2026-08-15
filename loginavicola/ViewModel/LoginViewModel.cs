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

        private string _username;
        public string Username
        {
            get => _username;
            set { _username = value; OnPropertyChanged(); }
        }

        private string _password;
        public string Password
        {
            get => _password;
            set { _password = value; OnPropertyChanged(); }
        }

        public ICommand LoginCommand { get; }

        public LoginViewModel()
        {
            LoginCommand = new ViewModelCommand(ExecuteLoginCommand);
        }

        private void ExecuteLoginCommand(object obj)
        {
            // 🔥 LIMPIAR ESPACIOS (CLAVE)
            string user = Username?.Trim();
            string pass = Password?.Trim();

            var usuario = database.ValidarLogin(user, pass);

            if (usuario != null)
            {
                UserSession.UsuarioActual = usuario;

                var mainWin = new MainWindow();

                if (mainWin.DataContext is MainViewModel mainVM)
                {
                    mainVM.CurrentUserAccount = usuario;
                }
                mainWin.Show();

                // cerrar ventana login
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