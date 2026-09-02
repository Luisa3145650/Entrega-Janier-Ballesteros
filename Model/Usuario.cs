using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel;

namespace loginavicola.Model
{
    public class Usuario : INotifyPropertyChanged
    {
        private int _idUsuario;
        private string _nombres = string.Empty;
        public string Nombres
        {
            get => _nombres;
            set
            {
                _nombres = value;
                OnPropertyChanged(nameof(Nombres));
            }
        }
        private string _apellidos = string.Empty;
        private string _username = string.Empty; // Antes era NombreUsuario
        private string _documento = string.Empty;
        private string _email = string.Empty;
        private string _rol = "Usuario";
        private bool _permisoInicio = true;
        // ... (puedes agregar campos privados para los demás si lo deseas)

        public int IdUsuario { get; set; }
        public string Apellidos { get; set; } = string.Empty;

        // ✅ Cambiado a Username para solucionar los errores CS1061
        public string Username
        {
            get => _username;
            set { _username = value; OnPropertyChanged(nameof(Username)); }
        }

        public string Documento { get; set; } = string.Empty;
        public string Telefono { get; set; } = string.Empty;
        public string Direccion { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string Rol { get; set; } = "Usuario";
        public DateTime FechaCreacion { get; set; } = DateTime.Now;
        public bool Activo { get; set; } = true;

        public string NombreCompleto => $"{Nombres} {Apellidos}";

        // ✅ Permisos con notificación (para que los CheckBoxes reaccionen)
        private bool _pInicio = true;
        public bool PermisoInicio { get => _pInicio; set { _pInicio = value; OnPropertyChanged(nameof(PermisoInicio)); } }

        private bool _pLotes;
        public bool PermisoLotes { get => _pLotes; set { _pLotes = value; OnPropertyChanged(nameof(PermisoLotes)); } }

        private bool _pProduccion;
        public bool PermisoProduccion { get => _pProduccion; set { _pProduccion = value; OnPropertyChanged(nameof(PermisoProduccion)); } }

        private bool _pAlimentacion;
        public bool PermisoAlimentacion { get => _pAlimentacion; set { _pAlimentacion = value; OnPropertyChanged(nameof(PermisoAlimentacion)); } }

        private bool _pExportarDatos;
        public bool PermisoExportarDatos { get => _pExportarDatos; set { _pExportarDatos = value; OnPropertyChanged(nameof(PermisoExportarDatos)); } }
        public bool ExportarDatos { get => PermisoExportarDatos; set => PermisoExportarDatos = value; }

        private bool _pDiagnostico;
        public bool PermisoDiagnostico { get => _pDiagnostico; set { _pDiagnostico = value; OnPropertyChanged(nameof(PermisoDiagnostico)); } }

        private bool _pInventario;
        public bool PermisoInventario { get => _pInventario; set { _pInventario = value; OnPropertyChanged(nameof(PermisoInventario)); } }

        private bool _pGestion;
        public bool PermisoGestionUsuarios { get => _pGestion; set { _pGestion = value; OnPropertyChanged(nameof(PermisoGestionUsuarios)); } }

        // ✅ Evento necesario para MVVM
        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}