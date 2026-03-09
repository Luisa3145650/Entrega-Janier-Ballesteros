using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using System.Linq;
using System.Windows;
using loginavicola.Database;
using loginavicola.Model;

namespace loginavicola.ViewModel
{
    public class InventarioViewModel : INotifyPropertyChanged
    {
        private readonly InventarioDatabase database;

        public InventarioViewModel()
        {
            database = new InventarioDatabase();

            // Inicializar colecciones
            ItemsInventario = new ObservableCollection<ItemInventario>();
            Categorias = new ObservableCollection<string>
            {
                "Alimento",
                "Medicamento",
                "Suplemento",
                "Equipo",
                "Limpieza",
                "Herramienta",
                "Otro"
            };

            // Inicializar comandos
            EditarItemCommand = new RelayCommand(EditarItem);
            EliminarItemCommand = new RelayCommand(EliminarItem);
            AjustarStockCommand = new RelayCommand(AjustarStock);

            // Cargar datos
            CargarDatos();
        }

        // Propiedades para las tarjetas
        private int _totalProductos;
        public int TotalProductos
        {
            get => _totalProductos;
            set { _totalProductos = value; OnPropertyChanged(nameof(TotalProductos)); }
        }

        private int _stockBajo;
        public int StockBajo
        {
            get => _stockBajo;
            set { _stockBajo = value; OnPropertyChanged(nameof(StockBajo)); }
        }

        private int _stockOptimo;
        public int StockOptimo
        {
            get => _stockOptimo;
            set { _stockOptimo = value; OnPropertyChanged(nameof(StockOptimo)); }
        }

        private decimal _valorTotal;
        public decimal ValorTotal
        {
            get => _valorTotal;
            set { _valorTotal = value; OnPropertyChanged(nameof(ValorTotal)); }
        }

        // Colecciones
        public ObservableCollection<ItemInventario> ItemsInventario { get; set; }
        public ObservableCollection<string> Categorias { get; set; }

        private ItemInventario _itemActual = new ItemInventario();
        public ItemInventario ItemActual
        {
            get => _itemActual;
            set { _itemActual = value; OnPropertyChanged(nameof(ItemActual)); }
        }

        private string _textoBusqueda = string.Empty;
        public string TextoBusqueda
        {
            get => _textoBusqueda;
            set
            {
                _textoBusqueda = value;
                OnPropertyChanged(nameof(TextoBusqueda));
                FiltrarItems();
            }
        }

        private bool _esEdicion;

        // Comandos
        public ICommand EditarItemCommand { get; }
        public ICommand EliminarItemCommand { get; }
        public ICommand AjustarStockCommand { get; }

        // Métodos
        public void CargarDatos()
        {
            ItemsInventario.Clear();
            var items = database.ObtenerTodosItems();

            foreach (var item in items)
            {
                ItemsInventario.Add(item);
            }

            ActualizarEstadisticas();
        }

        private void ActualizarEstadisticas()
        {
            TotalProductos = database.ObtenerTotalProductos();
            StockBajo = database.ObtenerStockBajo();
            StockOptimo = database.ObtenerStockOptimo();
            ValorTotal = database.ObtenerValorTotal();
        }

        public bool GuardarItem()
        {
            if (ValidarItem())
            {
                bool resultado;

                if (_esEdicion)
                {
                    resultado = database.ActualizarItem(ItemActual);
                    if (resultado)
                    {
                        MessageBox.Show("Producto actualizado exitosamente", "Éxito",
                            MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
                else
                {
                    resultado = database.InsertarItem(ItemActual);
                    if (resultado)
                    {
                        MessageBox.Show("Producto registrado exitosamente", "Éxito",
                            MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }

                if (resultado)
                {
                    CargarDatos();
                    LimpiarFormulario();
                    return true;
                }
            }
            return false;
        }

        private bool ValidarItem()
        {
            if (string.IsNullOrWhiteSpace(ItemActual.Nombre))
            {
                MessageBox.Show("Debe ingresar un nombre", "Validación",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (string.IsNullOrWhiteSpace(ItemActual.Categoria))
            {
                MessageBox.Show("Debe seleccionar una categoría", "Validación",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (ItemActual.CostoUnitario < 0)
            {
                MessageBox.Show("El costo no puede ser negativo", "Validación",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (ItemActual.StockMinimo < 0 || ItemActual.StockMaximo < 0)
            {
                MessageBox.Show("Los valores de stock no pueden ser negativos", "Validación",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (ItemActual.StockMaximo < ItemActual.StockMinimo)
            {
                MessageBox.Show("El stock máximo debe ser mayor al stock mínimo", "Validación",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            return true;
        }

        public void LimpiarFormulario()
        {
            ItemActual = new ItemInventario();
            _esEdicion = false;
        }

        public void PrepararEdicion(ItemInventario item)
        {
            _esEdicion = true;
            ItemActual = new ItemInventario
            {
                IdItem = item.IdItem,
                Nombre = item.Nombre,
                Categoria = item.Categoria,
                CostoUnitario = item.CostoUnitario,
                Ubicacion = item.Ubicacion,
                FechaCaducidad = item.FechaCaducidad,
                StockMinimo = item.StockMinimo,
                StockMaximo = item.StockMaximo,
                CantidadStock = item.CantidadStock,
                Observaciones = item.Observaciones
            };
        }

        private void EditarItem(object parameter)
        {
            if (parameter is ItemInventario item)
            {
                PrepararEdicion(item);
                // El modal se abrirá desde el code-behind
            }
        }

        private void EliminarItem(object parameter)
        {
            if (parameter is ItemInventario item)
            {
                var resultado = MessageBox.Show(
                    $"¿Está seguro de eliminar el producto '{item.Nombre}'?",
                    "Confirmar eliminación",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (resultado == MessageBoxResult.Yes)
                {
                    if (database.EliminarItem(item.IdItem))
                    {
                        MessageBox.Show("Producto eliminado exitosamente", "Éxito",
                            MessageBoxButton.OK, MessageBoxImage.Information);
                        CargarDatos();
                    }
                }
            }
        }

        private void AjustarStock(object parameter)
        {
            // Esta funcionalidad se puede implementar con un mini-modal adicional
            MessageBox.Show("Funcionalidad de ajuste de stock pendiente", "Info");
        }

        private void FiltrarItems()
        {
            if (string.IsNullOrWhiteSpace(TextoBusqueda))
            {
                CargarDatos();
                return;
            }

            var itemsFiltrados = database.ObtenerTodosItems()
                .Where(i =>
                    i.Nombre.ToLower().Contains(TextoBusqueda.ToLower()) ||
                    i.Categoria.ToLower().Contains(TextoBusqueda.ToLower()) ||
                    i.Ubicacion.ToLower().Contains(TextoBusqueda.ToLower()) ||
                    i.IdItem.ToString().Contains(TextoBusqueda)
                ).ToList();

            ItemsInventario.Clear();
            foreach (var item in itemsFiltrados)
            {
                ItemsInventario.Add(item);
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}