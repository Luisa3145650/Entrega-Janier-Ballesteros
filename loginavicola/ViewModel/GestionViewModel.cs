using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using loginavicola.Database;
using loginavicola.Model;

namespace loginavicola.ViewModel
{
    public class GestionViewModel : INotifyPropertyChanged
    {
        private readonly UsuarioDatabase database;

        public GestionViewModel()
        {
            database = new UsuarioDatabase();
            Usuarios = new ObservableCollection<Usuario>();
            UsuarioActual = new Usuario();
            UsuarioSeleccionado = new Usuario(); // Inicializamos para evitar errores

            CargarUsuarios();
        }

        // ✅ COLECCIONES
        public ObservableCollection<Usuario> Usuarios { get; set; }

        private Usuario _usuarioActual = new Usuario();
        public Usuario UsuarioActual
        {
            get => _usuarioActual;
            set { _usuarioActual = value; OnPropertyChanged(nameof(UsuarioActual)); }
        }

        private Usuario _usuarioSeleccionado = new Usuario();
        public Usuario UsuarioSeleccionado
        {
            get => _usuarioSeleccionado;
            set
            {
                _usuarioSeleccionado = value;
                OnPropertyChanged(nameof(UsuarioSeleccionado));
                // Al seleccionar un usuario, notificamos que todos sus permisos cambiaron
                NotificarCambioPermisos();
            }
        }

        // ============================================================
        // ✅ PROPIEDADES PUENTE PARA LOS PERMISOS (Soluciona errores de Binding)
        // Estas propiedades conectan los CheckBoxes con el UsuarioSeleccionado
        // ============================================================

        public bool PermisoInicio
        {
            get => UsuarioSeleccionado?.PermisoInicio ?? false;
            set { if (UsuarioSeleccionado != null) { UsuarioSeleccionado.PermisoInicio = value; OnPropertyChanged(nameof(PermisoInicio)); } }
        }

        public bool PermisoLotes
        {
            get => UsuarioSeleccionado?.PermisoLotes ?? false;
            set { if (UsuarioSeleccionado != null) { UsuarioSeleccionado.PermisoLotes = value; OnPropertyChanged(nameof(PermisoLotes)); } }
        }

        public bool PermisoProduccion
        {
            get => UsuarioSeleccionado?.PermisoProduccion ?? false;
            set { if (UsuarioSeleccionado != null) { UsuarioSeleccionado.PermisoProduccion = value; OnPropertyChanged(nameof(PermisoProduccion)); } }
        }

        public bool PermisoAlimentacion
        {
            get => UsuarioSeleccionado?.PermisoAlimentacion ?? false;
            set { if (UsuarioSeleccionado != null) { UsuarioSeleccionado.PermisoAlimentacion = value; OnPropertyChanged(nameof(PermisoAlimentacion)); } }
        }

        public bool PermisoEntregas
        {
            get => UsuarioSeleccionado?.PermisoEntregas ?? false;
            set { if (UsuarioSeleccionado != null) { UsuarioSeleccionado.PermisoEntregas = value; OnPropertyChanged(nameof(PermisoEntregas)); } }
        }

        public bool PermisoDiagnostico
        {
            get => UsuarioSeleccionado?.PermisoDiagnostico ?? false;
            set { if (UsuarioSeleccionado != null) { UsuarioSeleccionado.PermisoDiagnostico = value; OnPropertyChanged(nameof(PermisoDiagnostico)); } }
        }

        public bool PermisoInventario
        {
            get => UsuarioSeleccionado?.PermisoInventario ?? false;
            set { if (UsuarioSeleccionado != null) { UsuarioSeleccionado.PermisoInventario = value; OnPropertyChanged(nameof(PermisoInventario)); } }
        }

        public bool PermisoGestionUsuarios
        {
            get => UsuarioSeleccionado?.PermisoGestionUsuarios ?? false;
            set { if (UsuarioSeleccionado != null) { UsuarioSeleccionado.PermisoGestionUsuarios = value; OnPropertyChanged(nameof(PermisoGestionUsuarios)); } }
        }

        private void NotificarCambioPermisos()
        {
            OnPropertyChanged(nameof(PermisoInicio));
            OnPropertyChanged(nameof(PermisoLotes));
            OnPropertyChanged(nameof(PermisoProduccion));
            OnPropertyChanged(nameof(PermisoAlimentacion));
            OnPropertyChanged(nameof(PermisoEntregas));
            OnPropertyChanged(nameof(PermisoDiagnostico));
            OnPropertyChanged(nameof(PermisoInventario));
            OnPropertyChanged(nameof(PermisoGestionUsuarios));
        }

        // ============================================================
        // MÉTODOS
        // ============================================================
        public void CargarUsuarios()
        {
            Usuarios.Clear();
            var usuarios = database.ObtenerTodosLosUsuarios();
            foreach (var usuario in usuarios)
            {
                Usuarios.Add(usuario);
            }
        }

        public bool GuardarUsuario(string password, string confirmPassword)
        {
            if (string.IsNullOrWhiteSpace(UsuarioActual.Nombres) ||
                string.IsNullOrWhiteSpace(UsuarioActual.Apellidos) ||
                string.IsNullOrWhiteSpace(UsuarioActual.Documento) ||
                string.IsNullOrWhiteSpace(UsuarioActual.Email))
            {
                MessageBox.Show("Todos los campos obligatorios deben estar completos", "Validación",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            // IMPORTANTE: Asegúrate de que UsuarioActual.Username tenga un valor
            if (string.IsNullOrWhiteSpace(UsuarioActual.Username))
            {
                // Como solución rápida, asignamos el email como username si está vacío
                UsuarioActual.Username = UsuarioActual.Email;
            }

            if (string.IsNullOrWhiteSpace(password))
            {
                MessageBox.Show("Debe ingresar una contraseña", "Validación",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (password != confirmPassword)
            {
                MessageBox.Show("Las contraseñas no coinciden", "Validación",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (password.Length < 6)
            {
                MessageBox.Show("La contraseña debe tener al menos 6 caracteres", "Validación",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (database.InsertarUsuario(UsuarioActual, password))
            {
                MessageBox.Show("Usuario guardado con éxito", "Éxito");
                CargarUsuarios();
                UsuarioActual = new Usuario();
                return true;
            }

            return false;
        }

        public bool ActualizarPermisos()
        {
            if (UsuarioSeleccionado == null) return false;

            if (database.ActualizarPermisos(UsuarioSeleccionado))
            {
                MessageBox.Show("Permisos actualizados correctamente", "Éxito");
                CargarUsuarios();
                return true;
            }
            return false;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}