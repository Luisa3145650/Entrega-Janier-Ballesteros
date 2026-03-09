using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System;
using System.ComponentModel;

namespace loginavicola.Model
{
    public class Venta : INotifyPropertyChanged
    {
        private int _idVenta;
        private DateTime _fecha;
        private string _cliente = string.Empty;
        private string _tipoVenta = string.Empty;
        private string _categoria = string.Empty;
        private int _cantidad;
        private decimal _costoTotal;
        private string _estado = "Pendiente";
        private string _observaciones = string.Empty;
        private string _metodoPago = string.Empty;

        public int IdVenta
        {
            get => _idVenta;
            set
            {
                _idVenta = value;
                OnPropertyChanged(nameof(IdVenta));
            }
        }

        public DateTime Fecha
        {
            get => _fecha;
            set
            {
                _fecha = value;
                OnPropertyChanged(nameof(Fecha));
            }
        }

        public string Cliente
        {
            get => _cliente;
            set
            {
                _cliente = value ?? string.Empty;
                OnPropertyChanged(nameof(Cliente));
            }
        }

        public string TipoVenta
        {
            get => _tipoVenta;
            set
            {
                _tipoVenta = value ?? string.Empty;
                OnPropertyChanged(nameof(TipoVenta));
            }
        }

        public string Categoria
        {
            get => _categoria;
            set
            {
                _categoria = value ?? string.Empty;
                OnPropertyChanged(nameof(Categoria));
            }
        }

        public int Cantidad
        {
            get => _cantidad;
            set
            {
                _cantidad = value;
                OnPropertyChanged(nameof(Cantidad));
            }
        }

        public decimal CostoTotal
        {
            get => _costoTotal;
            set
            {
                _costoTotal = value;
                OnPropertyChanged(nameof(CostoTotal));
            }
        }

        public string Estado
        {
            get => _estado;
            set
            {
                _estado = value ?? "Pendiente";
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

        public string MetodoPago
        {
            get => _metodoPago;
            set
            {
                _metodoPago = value ?? string.Empty;
                OnPropertyChanged(nameof(MetodoPago));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
