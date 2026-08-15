using loginavicola;
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
        // Usamos la ruta completa del modelo para evitar ambigüedades
        private loginavicola.Model.Usuario _currentUserAccount;

        // Propiedades que el XAML lee para mostrar en la parte superior
        public string DisplayName => CurrentUserAccount?.NombreCompleto ?? "Usuario Invitado";
        public string DisplayRol => CurrentUserAccount?.Rol ?? "Sin Rol";

        public loginavicola.Model.Usuario CurrentUserAccount
        {
            get => _currentUserAccount;
            set
            {
                _currentUserAccount = value;
                // Notificamos que el usuario cambió
                OnPropertyChanged(nameof(CurrentUserAccount));
                // Notificamos que las propiedades dependientes (Nombre y Rol) también deben refrescarse
                OnPropertyChanged(nameof(DisplayName));
                OnPropertyChanged(nameof(DisplayRol));
            }
        }

        public MainViewModel()
        {
            // Al crear el ViewModel, intentamos cargar los datos de la sesión inmediatamente
            LoadCurrentUserData();
        }

        public void LoadCurrentUserData()
        {
            // SOLUCIÓN A LA AMBIGÜEDAD: Especificamos que busque en el Namespace .Model
            var user = loginavicola.UserSession.UsuarioActual;

            if (user != null)
            {
                this.CurrentUserAccount = user;
            }
        }

        // --- Implementación de Notificación de Cambios ---
        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}