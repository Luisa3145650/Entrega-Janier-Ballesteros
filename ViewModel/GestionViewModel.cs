using loginavicola.Database;
using loginavicola.Model;
using System.Collections.ObjectModel;
using System.Windows;

namespace loginavicola.ViewModel
{
    public class GestionViewModel : ViewModelBase
    {
        private readonly UsuarioDatabase database = new UsuarioDatabase();

        public ObservableCollection<Usuario> Usuarios { get; set; } = new ObservableCollection<Usuario>();

        private Usuario _usuarioActual = new Usuario();
        public Usuario UsuarioActual
        {
            get => _usuarioActual;
            set
            {
                _usuarioActual = value;
                OnPropertyChanged();
            }
        }

        private Usuario _usuarioSeleccionado = new Usuario();
        public Usuario UsuarioSeleccionado
        {
            get => _usuarioSeleccionado;
            set
            {
                _usuarioSeleccionado = value;
                OnPropertyChanged();
                NotificarCambioPermisos();
            }
        }

        public GestionViewModel()
        {
            CargarUsuarios();
        }

        // 🔹 Cargar usuarios
        public void CargarUsuarios()
        {
            Usuarios.Clear();
            var lista = database.ObtenerTodosLosUsuarios();

            foreach (var usuario in lista)
            {
                Usuarios.Add(usuario);
            }
        }

        // 🔥 GUARDAR USUARIO (CORREGIDO)
        public bool GuardarUsuario(string password, string confirmPassword)
        {
            if (string.IsNullOrWhiteSpace(UsuarioActual.Nombres) ||
                string.IsNullOrWhiteSpace(UsuarioActual.Email))
            {
                MessageBox.Show("Complete los campos obligatorios");
                return false;
            }

            if (password != confirmPassword)
            {
                MessageBox.Show("Las contraseñas no coinciden");
                return false;
            }

            // 🔥 LIMPIAR DATOS (CLAVE)
            UsuarioActual.Email = UsuarioActual.Email.Trim();
            UsuarioActual.Username = string.IsNullOrEmpty(UsuarioActual.Username)
                ? UsuarioActual.Email
                : UsuarioActual.Username.Trim();

            bool guardado = database.InsertarUsuario(UsuarioActual, password.Trim());

            if (guardado)
            {
                MessageBox.Show("Usuario guardado correctamente");
                CargarUsuarios();
                UsuarioActual = new Usuario();
            }

            return guardado;
        }

        public bool ActualizarPermisos()
        {
            if (UsuarioSeleccionado == null)
                return false;

            return database.ActualizarPermisos(UsuarioSeleccionado);
        }

        // 🔹 PERMISOS (para los CheckBox)
        public bool PermisoInicio
        {
            get => UsuarioSeleccionado?.PermisoInicio ?? false;
            set { UsuarioSeleccionado.PermisoInicio = value; OnPropertyChanged(); }
        }

        public bool PermisoLotes
        {
            get => UsuarioSeleccionado?.PermisoLotes ?? false;
            set { UsuarioSeleccionado.PermisoLotes = value; OnPropertyChanged(); }
        }

        public bool PermisoProduccion
        {
            get => UsuarioSeleccionado?.PermisoProduccion ?? false;
            set { UsuarioSeleccionado.PermisoProduccion = value; OnPropertyChanged(); }
        }

        public bool PermisoAlimentacion
        {
            get => UsuarioSeleccionado?.PermisoAlimentacion ?? false;
            set { UsuarioSeleccionado.PermisoAlimentacion = value; OnPropertyChanged(); }
        }

        public bool PermisoExportarDatos
        {
            get => UsuarioSeleccionado?.PermisoExportarDatos ?? false;
            set { UsuarioSeleccionado.PermisoExportarDatos = value; OnPropertyChanged(); }
        }

        public bool PermisoDiagnostico
        {
            get => UsuarioSeleccionado?.PermisoDiagnostico ?? false;
            set { UsuarioSeleccionado.PermisoDiagnostico = value; OnPropertyChanged(); }
        }

        public bool PermisoInventario
        {
            get => UsuarioSeleccionado?.PermisoInventario ?? false;
            set { UsuarioSeleccionado.PermisoInventario = value; OnPropertyChanged(); }
        }

        public bool PermisoGestionUsuarios
        {
            get => UsuarioSeleccionado?.PermisoGestionUsuarios ?? false;
            set { UsuarioSeleccionado.PermisoGestionUsuarios = value; OnPropertyChanged(); }
        }

        private void NotificarCambioPermisos()
        {
            OnPropertyChanged(nameof(PermisoInicio));
            OnPropertyChanged(nameof(PermisoLotes));
            OnPropertyChanged(nameof(PermisoProduccion));
            OnPropertyChanged(nameof(PermisoAlimentacion));
            OnPropertyChanged(nameof(PermisoExportarDatos));
            OnPropertyChanged(nameof(PermisoDiagnostico));
            OnPropertyChanged(nameof(PermisoInventario));
            OnPropertyChanged(nameof(PermisoGestionUsuarios));
        }
    }
}