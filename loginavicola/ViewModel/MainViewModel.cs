using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using loginavicola.Model;

namespace loginavicola.ViewModel
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private Usuario _currentUserAccount;

        public string DisplayName => CurrentUserAccount?.NombreCompleto ?? "Usuario Invitado";
        public string DisplayRol => CurrentUserAccount?.Rol ?? "Sin Rol";

        public Usuario CurrentUserAccount
        {
            get => _currentUserAccount;
            set
            {
                _currentUserAccount = value;
                OnPropertyChanged(nameof(CurrentUserAccount));

                OnPropertyChanged(nameof(DisplayName));
                OnPropertyChanged(nameof(DisplayRol));


            }
        }


        public MainViewModel()
        {
            // Cargar los datos del usuario desde la sesión que guardamos al hacer login
            LoadCurrentUserData();
        }

        public void LoadCurrentUserData()
        {
            var user = UserSession.UsuarioActual;
            if (user != null)
            {
                this.CurrentUserAccount = user;
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }


    }
}