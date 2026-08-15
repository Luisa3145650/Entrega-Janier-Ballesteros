using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel;

namespace loginavicola.Models
{
    public class LoteGallinas : INotifyPropertyChanged
    {
        private string _idLote = string.Empty;
        private string _raza = string.Empty;
        private int _cantidadGallinas;
        private DateTime _fechaIncorporacion;
        private string _granjaOrigen = string.Empty;
        private string _observaciones = string.Empty;
        private EstadoLote _estado;

        public string IdLote
        {
            get => _idLote;
            set { _idLote = value ?? string.Empty; OnPropertyChanged(nameof(IdLote)); }
        }

        public string Raza
        {
            get => _raza;
            set { _raza = value ?? string.Empty; OnPropertyChanged(nameof(Raza)); }
        }

        public int CantidadGallinas
        {
            get => _cantidadGallinas;
            set { _cantidadGallinas = value; OnPropertyChanged(nameof(CantidadGallinas)); }
        }

        public DateTime FechaIncorporacion
        {
            get => _fechaIncorporacion;
            set { _fechaIncorporacion = value; OnPropertyChanged(nameof(FechaIncorporacion)); }
        }

        public string GranjaOrigen
        {
            get => _granjaOrigen;
            set { _granjaOrigen = value ?? string.Empty; OnPropertyChanged(nameof(GranjaOrigen)); }
        }

        public string Observaciones
        {
            get => _observaciones;
            set { _observaciones = value ?? string.Empty; OnPropertyChanged(nameof(Observaciones)); }
        }

        public EstadoLote Estado
        {
            get => _estado;
            set { _estado = value; OnPropertyChanged(nameof(Estado)); }
        }

        public LoteGallinas()
        {
            _fechaIncorporacion = DateTime.Today;
            _estado = EstadoLote.Activo;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public enum EstadoLote
    {
        Activo,
        Inactivo,
        Vendido,
        Descarte
    }
}

