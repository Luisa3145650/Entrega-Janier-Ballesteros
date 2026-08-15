using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel;

namespace loginavicola.Model
{
    public class ClasificacionProduccion : INotifyPropertyChanged
    {
        private int _idClasificacion;
        private int _idLote;
        private DateTime _fecha;
        private string _horaInicio = string.Empty;
        private string _horaFin = string.Empty;
        private string _recolector = string.Empty;
        private string _tipoClasificacion = string.Empty; // "Manual" o "Automática"
        private string _estadoSesion = "Abierta";
        private int _jumbo;
        private int _aaa;
        private int _aa;
        private int _a;
        private int _b;
        private int _c;
        private int _total;
        private string _observaciones = string.Empty;

        public int IdClasificacion
        {
            get => _idClasificacion;
            set { _idClasificacion = value; OnPropertyChanged(nameof(IdClasificacion)); }
        }

        public int IdLote
        {
            get => _idLote;
            set { _idLote = value; OnPropertyChanged(nameof(IdLote)); }
        }

        public DateTime Fecha
        {
            get => _fecha;
            set { _fecha = value; OnPropertyChanged(nameof(Fecha)); }
        }

        public string HoraInicio
        {
            get => _horaInicio;
            set { _horaInicio = value ?? string.Empty; OnPropertyChanged(nameof(HoraInicio)); }
        }

        public string HoraFin
        {
            get => _horaFin;
            set { _horaFin = value ?? string.Empty; OnPropertyChanged(nameof(HoraFin)); }
        }

        public string Recolector
        {
            get => _recolector;
            set { _recolector = value ?? string.Empty; OnPropertyChanged(nameof(Recolector)); }
        }

        public string TipoClasificacion
        {
            get => _tipoClasificacion;
            set { _tipoClasificacion = value ?? string.Empty; OnPropertyChanged(nameof(TipoClasificacion)); }
        }

        public string EstadoSesion
        {
            get => _estadoSesion;
            set { _estadoSesion = value ?? string.Empty; OnPropertyChanged(nameof(EstadoSesion)); }
        }

        public int Jumbo
        {
            get => _jumbo;
            set { _jumbo = value; OnPropertyChanged(nameof(Jumbo)); CalcularTotal(); }
        }

        public int AAA
        {
            get => _aaa;
            set { _aaa = value; OnPropertyChanged(nameof(AAA)); CalcularTotal(); }
        }

        public int AA
        {
            get => _aa;
            set { _aa = value; OnPropertyChanged(nameof(AA)); CalcularTotal(); }
        }

        public int A
        {
            get => _a;
            set { _a = value; OnPropertyChanged(nameof(A)); CalcularTotal(); }
        }

        public int B
        {
            get => _b;
            set { _b = value; OnPropertyChanged(nameof(B)); CalcularTotal(); }
        }

        public int C
        {
            get => _c;
            set { _c = value; OnPropertyChanged(nameof(C)); CalcularTotal(); }
        }

        public int Total
        {
            get => _total;
            set { _total = value; OnPropertyChanged(nameof(Total)); }
        }

        public string Observaciones
        {
            get => _observaciones;
            set { _observaciones = value ?? string.Empty; OnPropertyChanged(nameof(Observaciones)); }
        }

        // Propiedad computada para mostrar la fecha y hora de inicio juntas
        public string FechaHoraCompleta => $"{Fecha:dd/MM/yyyy} {HoraInicio}";

        private void CalcularTotal()
        {
            Total = Jumbo + AAA + AA + A + B + C;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}