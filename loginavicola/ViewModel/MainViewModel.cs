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

        public Usuario CurrentUserAccount
        {
            get => _currentUserAccount;
            set
            {
                _currentUserAccount = value;
                OnPropertyChanged(nameof(CurrentUserAccount));
            }
        }


        public MainViewModel()
        {
            // Cargar los datos del usuario desde la sesión que guardamos al hacer login
            LoadCurrentUserData();
        }

        private void LoadCurrentUserData()
        {
            var user = UserSession.UsuarioActual;
            if (user != null)
            {
                CurrentUserAccount = user;
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}