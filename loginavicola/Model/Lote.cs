using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel;

namespace loginavicola.Model
{
    public class Lote : INotifyPropertyChanged
    {
        private int _idLote;
        private string _raza = string.Empty;
        private int _cantidadGallinas;
        private DateTime _fechaIncorporacion;
        private string _granjaOrigen = string.Empty;
        private string _estado = string.Empty;
        private string _observaciones = string.Empty;

        public int IdLote
        {
            get => _idLote;
            set
            {
                _idLote = value;
                OnPropertyChanged(nameof(IdLote));
            }
        }

        public string Raza
        {
            get => _raza;
            set
            {
                _raza = value ?? string.Empty;
                OnPropertyChanged(nameof(Raza));
                System.Diagnostics.Debug.WriteLine($"Raza cambiada a: '{_raza}'");
            }
        }

        public int CantidadGallinas
        {
            get => _cantidadGallinas;
            set
            {
                _cantidadGallinas = value;
                OnPropertyChanged(nameof(CantidadGallinas));
                System.Diagnostics.Debug.WriteLine($"Cantidad cambiada a: {_cantidadGallinas}");
            }
        }

        public DateTime FechaIncorporacion
        {
            get => _fechaIncorporacion;
            set
            {
                _fechaIncorporacion = value;
                OnPropertyChanged(nameof(FechaIncorporacion));
            }
        }

        public string GranjaOrigen
        {
            get => _granjaOrigen;
            set
            {
                _granjaOrigen = value ?? string.Empty;
                OnPropertyChanged(nameof(GranjaOrigen));
            }
        }

        public string Estado
        {
            get => _estado;
            set
            {
                _estado = value ?? string.Empty;
                OnPropertyChanged(nameof(Estado));
            }
        }

        public string Observaciones
        {
            get => _observaciones;
            set
            {
                _observaciones = value ?? string.Empty;
                OnPropertyChanged(nameof(Observaciones));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}