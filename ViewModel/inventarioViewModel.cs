using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using System.Windows;
using loginavicola.Database;
using loginavicola.Model;

namespace loginavicola.ViewModel
{
    public class InventarioViewModel : INotifyPropertyChanged
    {
        private readonly InventarioDatabase database;
        private List<ItemInventario> _todosLosFiltrados = new();

        public InventarioViewModel()
        {
            database = new InventarioDatabase();

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

            OpcionesTamanoPagina = new ObservableCollection<int> { 5, 10, 15, 20, 50 };
            _tamanoPagina = 10;
            _paginaActual = 1;

            EditarItemCommand = new RelayCommand(EditarItem);
            EliminarItemCommand = new RelayCommand(EliminarItem);
            AjustarStockCommand = new RelayCommand(AjustarStock);

            PaginaAnteriorCommand = new RelayCommand(_ => CambiarPagina(-1), _ => PaginaActual > 1);
            PaginaSiguienteCommand = new RelayCommand(_ => CambiarPagina(1), _ => PaginaActual < TotalPaginas);
            VerDetalleAlertasCommand = new RelayCommand(_ => AbrirModalAlertas());

            CargarDatos();
        }

        private void AbrirModalAlertas()
        {
            var alertas = loginavicola.Helpers.AlertasService.ObtenerAlertasActivas();
            var modal = new loginavicola.View.AlertasModalWindow(alertas);
            if (Application.Current != null && Application.Current.MainWindow != null)
            {
                modal.Owner = Application.Current.MainWindow;
            }
            modal.ShowDialog();
        }

        // ── Propiedades de Paginación ────────────────────────────────────
        public ObservableCollection<int> OpcionesTamanoPagina { get; }

        private int _paginaActual;
        public int PaginaActual
        {
            get => _paginaActual;
            set
            {
                _paginaActual = value;
                OnPropertyChanged(nameof(PaginaActual));
                OnPropertyChanged(nameof(InfoPagina));
                OnPropertyChanged(nameof(CanGoPrevious));
                OnPropertyChanged(nameof(CanGoNext));
                AplicarPagina();
            }
        }

        private int _tamanoPagina;
        public int TamanoPagina
        {
            get => _tamanoPagina;
            set
            {
                _tamanoPagina = value;
                OnPropertyChanged(nameof(TamanoPagina));
                _paginaActual = 1;
                OnPropertyChanged(nameof(PaginaActual));
                RecalcularPaginas();
                AplicarPagina();
            }
        }

        private int _totalPaginas = 1;
        public int TotalPaginas
        {
            get => _totalPaginas;
            set
            {
                _totalPaginas = value;
                OnPropertyChanged(nameof(TotalPaginas));
                OnPropertyChanged(nameof(InfoPagina));
            }
        }

        public bool CanGoPrevious => PaginaActual > 1;
        public bool CanGoNext => PaginaActual < TotalPaginas;

        private int _totalRegistros;
        public int TotalRegistros
        {
            get => _totalRegistros;
            set { _totalRegistros = value; OnPropertyChanged(nameof(TotalRegistros)); }
        }

        private int _registrosInicio;
        public int RegistrosInicio
        {
            get => _registrosInicio;
            set { _registrosInicio = value; OnPropertyChanged(nameof(RegistrosInicio)); }
        }

        private int _registrosFin;
        public int RegistrosFin
        {
            get => _registrosFin;
            set { _registrosFin = value; OnPropertyChanged(nameof(RegistrosFin)); }
        }

        public string InfoPagina => $"Página {PaginaActual} de {TotalPaginas}  ({_todosLosFiltrados.Count} registros)";

        // ── Propiedades de estadísticas ──────────────────────────────────
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

        // ── Colecciones ──────────────────────────────────────────────────
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

        // ── Comandos ─────────────────────────────────────────────────────
        public ICommand EditarItemCommand { get; }
        public ICommand EliminarItemCommand { get; }
        public ICommand AjustarStockCommand { get; }
        public ICommand PaginaAnteriorCommand { get; }
        public ICommand PaginaSiguienteCommand { get; }
        public ICommand VerDetalleAlertasCommand { get; }

        // ── Métodos ──────────────────────────────────────────────────────
        public void CargarDatos()
        {
            var items = database.ObtenerTodosItems();

            _todosLosFiltrados = string.IsNullOrWhiteSpace(TextoBusqueda)
                ? items
                : items.Where(i =>
                    (i.Nombre?.ToLower().Contains(TextoBusqueda.ToLower()) ?? false) ||
                    (i.Categoria?.ToLower().Contains(TextoBusqueda.ToLower()) ?? false) ||
                    (i.Ubicacion?.ToLower().Contains(TextoBusqueda.ToLower()) ?? false) ||
                    i.IdItem.ToString().Contains(TextoBusqueda)
                ).ToList();

            _paginaActual = 1;
            RecalcularPaginas();
            AplicarPagina();
            ActualizarEstadisticas();
        }

        private void RecalcularPaginas()
        {
            TotalRegistros = _todosLosFiltrados.Count;
            TotalPaginas = Math.Max(1, (int)Math.Ceiling(TotalRegistros / (double)TamanoPagina));
            if (_paginaActual > TotalPaginas) _paginaActual = TotalPaginas;
            OnPropertyChanged(nameof(CanGoPrevious));
            OnPropertyChanged(nameof(CanGoNext));
        }

        private void AplicarPagina()
        {
            var pagina = _todosLosFiltrados
                .Skip((_paginaActual - 1) * TamanoPagina)
                .Take(TamanoPagina)
                .ToList();

            RegistrosInicio = TotalRegistros == 0 ? 0 : ((_paginaActual - 1) * TamanoPagina) + 1;
            RegistrosFin = Math.Min(_paginaActual * TamanoPagina, TotalRegistros);

            ItemsInventario.Clear();
            foreach (var item in pagina)
            {
                ItemsInventario.Add(item);
            }

            OnPropertyChanged(nameof(InfoPagina));
        }

        private void CambiarPagina(int delta)
        {
            var nueva = _paginaActual + delta;
            if (nueva >= 1 && nueva <= TotalPaginas)
                PaginaActual = nueva;
        }

        private void ActualizarEstadisticas()
        {
            TotalProductos = database.ObtenerTotalProductos();
            StockBajo = database.ObtenerStockBajo();
            StockOptimo = database.ObtenerStockOptimo();
            ValorTotal = database.ObtenerValorTotal();
        }

        // ── CRUD ──────────────────────────────────────────────────────────
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
            MessageBox.Show("Funcionalidad de ajuste de stock pendiente", "Info");
        }

        private void FiltrarItems()
        {
            CargarDatos();
        }

        // ── INotifyPropertyChanged ───────────────────────────────────────
        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}