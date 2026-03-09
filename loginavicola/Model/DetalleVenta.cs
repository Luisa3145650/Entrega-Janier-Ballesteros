using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel;

namespace loginavicola.Model
{
    public class DetalleVenta : INotifyPropertyChanged
    {
        private int _idDetalle;
        private int _idVenta;
        private int _idItem;
        private string _nombreProducto = string.Empty;
        private int _cantidad;
        private decimal _precioUnitario;
        private decimal _subtotal;

        public int IdDetalle
        {
            get => _idDetalle;
            set
            {
                _idDetalle = value;
                OnPropertyChanged(nameof(IdDetalle));
            }
        }

        public int IdVenta
        {
            get => _idVenta;
            set
            {
                _idVenta = value;
                OnPropertyChanged(nameof(IdVenta));
            }
        }

        public int IdItem
        {
            get => _idItem;
            set
            {
                _idItem = value;
                OnPropertyChanged(nameof(IdItem));
            }
        }

        public string NombreProducto
        {
            get => _nombreProducto;
            set
            {
                _nombreProducto = value ?? string.Empty;
                OnPropertyChanged(nameof(NombreProducto));
            }
        }

        public int Cantidad
        {
            get => _cantidad;
            set
            {
                _cantidad = value;
                OnPropertyChanged(nameof(Cantidad));
                CalcularSubtotal();
            }
        }

        public decimal PrecioUnitario
        {
            get => _precioUnitario;
            set
            {
                _precioUnitario = value;
                OnPropertyChanged(nameof(PrecioUnitario));
                CalcularSubtotal();
            }
        }

        public decimal Subtotal
        {
            get => _subtotal;
            set
            {
                _subtotal = value;
                OnPropertyChanged(nameof(Subtotal));
            }
        }

        private void CalcularSubtotal()
        {
            Subtotal = Cantidad * PrecioUnitario;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}