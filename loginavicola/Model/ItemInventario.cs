using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel;

namespace loginavicola.Model
{
    public class ItemInventario : INotifyPropertyChanged
    {
        private int _idItem;
        private string _nombre = string.Empty;
        private string _categoria = string.Empty;
        private decimal _costoUnitario;
        private string _ubicacion = string.Empty;
        private DateTime? _fechaCaducidad;
        private int _stockMinimo;
        private int _stockMaximo;
        private int _cantidadStock;
        private string _observaciones = string.Empty;

        public int IdItem
        {
            get => _idItem;
            set
            {
                _idItem = value;
                OnPropertyChanged(nameof(IdItem));
            }
        }

        public string Nombre
        {
            get => _nombre;
            set
            {
                _nombre = value ?? string.Empty;
                OnPropertyChanged(nameof(Nombre));
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

        public decimal CostoUnitario
        {
            get => _costoUnitario;
            set
            {
                _costoUnitario = value;
                OnPropertyChanged(nameof(CostoUnitario));
                OnPropertyChanged(nameof(ValorTotalStock));
            }
        }

        public string Ubicacion
        {
            get => _ubicacion;
            set
            {
                _ubicacion = value ?? string.Empty;
                OnPropertyChanged(nameof(Ubicacion));
            }
        }

        public DateTime? FechaCaducidad
        {
            get => _fechaCaducidad;
            set
            {
                _fechaCaducidad = value;
                OnPropertyChanged(nameof(FechaCaducidad));
                OnPropertyChanged(nameof(EstadoCaducidad));
            }
        }

        public int StockMinimo
        {
            get => _stockMinimo;
            set
            {
                _stockMinimo = value;
                OnPropertyChanged(nameof(StockMinimo));
                OnPropertyChanged(nameof(EstadoStock));
            }
        }

        public int StockMaximo
        {
            get => _stockMaximo;
            set
            {
                _stockMaximo = value;
                OnPropertyChanged(nameof(StockMaximo));
            }
        }

        public int CantidadStock
        {
            get => _cantidadStock;
            set
            {
                _cantidadStock = value;
                OnPropertyChanged(nameof(CantidadStock));
                OnPropertyChanged(nameof(EstadoStock));
                OnPropertyChanged(nameof(ValorTotalStock));
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

        // Propiedades calculadas
        public string EstadoStock
        {
            get
            {
                if (CantidadStock <= StockMinimo)
                    return "Bajo";
                else if (CantidadStock >= StockMaximo)
                    return "Exceso";
                else
                    return "Óptimo";
            }
        }

        public decimal ValorTotalStock => CostoUnitario * CantidadStock;

        public string EstadoCaducidad
        {
            get
            {
                if (!FechaCaducidad.HasValue)
                    return "Sin fecha";

                var diasParaCaducar = (FechaCaducidad.Value - DateTime.Now).Days;

                if (diasParaCaducar < 0)
                    return "Vencido";
                else if (diasParaCaducar <= 30)
                    return "Próximo a vencer";
                else
                    return "Vigente";
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
