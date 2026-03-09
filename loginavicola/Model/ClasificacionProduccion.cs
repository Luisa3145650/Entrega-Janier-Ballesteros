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
        private DateTime _fecha;
        private TimeSpan _hora;
        private string _recolector = string.Empty;
        private string _tipoClasificacion = string.Empty; // "Manual" o "Automática"
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

        public DateTime Fecha
        {
            get => _fecha;
            set { _fecha = value; OnPropertyChanged(nameof(Fecha)); }
        }

        public TimeSpan Hora
        {
            get => _hora;
            set { _hora = value; OnPropertyChanged(nameof(Hora)); }
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

        // Propiedad computada para mostrar fecha y hora juntas
        public string FechaHoraCompleta => $"{Fecha:dd/MM/yyyy} {Hora:hh\\:mm\\:ss}";

        private void CalcularTotal()
        {
            Total = Jumbo + AAA + AA + A + B + C;
        }

        public event PropertyChangedEventHandler? PropertyChanged; protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
