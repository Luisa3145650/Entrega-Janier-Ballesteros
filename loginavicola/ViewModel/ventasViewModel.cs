using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using loginavicola.Model;
using System.Windows;
using loginavicola.Database;

namespace loginavicola.ViewModel
{
    public class VentasViewModel : INotifyPropertyChanged
    {
        private readonly VentasDatabase ventasDatabase;
        private readonly InventarioDatabase inventarioDatabase;
        public ICommand EditarVentaCommand { get; }
        public ICommand EliminarVentaCommand { get; }

        public VentasViewModel()
        {
            ventasDatabase = new VentasDatabase();
            inventarioDatabase = new InventarioDatabase();
            EditarVentaCommand = new RelayCommand(EditarVenta);
            EliminarVentaCommand = new RelayCommand(EliminarVenta);

            // Inicializar colecciones
            ListaVentas = new ObservableCollection<Venta>();
            DetalleVenta = new ObservableCollection<DetalleVenta>();
            ProductosDisponibles = new ObservableCollection<ItemInventario>();

            TiposVenta = new ObservableCollection<string>
            {
                "Huevos",
                "Aves",
                "Pollos BB",
                "Otro"
            };

            // AGREGADO: Estados para el modal
            Estados = new ObservableCollection<string>
            {
                "Pendiente",
                "En Tránsito",
                "Entregado"
            };

            Categorias = new ObservableCollection<string>
            {
                "Jumbo", 
                "AAA",
                "AA",
                "A",
                "B", 
                "C"
            };

            Presentaciones = new ObservableCollection<string>
            {
                "Por Unidad",
                "Cubeta (30)",
                "Panal (15)",
                "Caja (360)",
                "Bandeja (12)"
            };

            MetodosPago = new ObservableCollection<string>
            {
                "Efectivo",
                "Transferencia",
                "Tarjeta",
                "Crédito"
            };

            // Inicializar comandos
            LimpiarVentaCommand = new RelayCommand(LimpiarVenta);
            FinalizarVentaCommand = new RelayCommand(FinalizarVenta);
            AgregarProductoCommand = new RelayCommand(AgregarProducto);
            EliminarDetalleCommand = new RelayCommand(EliminarDetalle);

            // AGREGADO: Comandos para el modal
            AbrirModalCommand = new RelayCommand(AbrirModal);
            CerrarModalCommand = new RelayCommand(CerrarModal);
            RegistrarCommand = new RelayCommand(RegistrarVenta);

            // Inicializar venta actual
            VentaActual = new Venta { Fecha = DateTime.Now };

            // Cargar datos
            CargarVentas();
            CargarProductosDisponibles();
            ActualizarEstadisticas();
        }

        // Propiedades
        public ObservableCollection<Venta> ListaVentas { get; set; }
        public ObservableCollection<DetalleVenta> DetalleVenta { get; set; }
        public ObservableCollection<ItemInventario> ProductosDisponibles { get; set; }
        public ObservableCollection<string> TiposVenta { get; set; }
        public ObservableCollection<string> Estados { get; set; }
        public ObservableCollection<string> Categorias { get; set; }
        public ObservableCollection<string> Presentaciones { get; set; }
        public ObservableCollection<string> MetodosPago { get; set; }

        // AGREGADO: Propiedad para controlar visibilidad del modal
        private bool _mostrarModal = false;
        public bool MostrarModal
        {
            get => _mostrarModal;
            set
            {
                _mostrarModal = value;
                OnPropertyChanged(nameof(MostrarModal));
            }
        }

        // AGREGADO: Propiedades para estadísticas
        private int _totalEntregas;
        public int TotalEntregas
        {
            get => _totalEntregas;
            set { _totalEntregas = value; OnPropertyChanged(nameof(TotalEntregas)); }
        }

        private int _pendientes;
        public int Pendientes
        {
            get => _pendientes;
            set { _pendientes = value; OnPropertyChanged(nameof(Pendientes)); }
        }

        private int _enTransito;
        public int EnTransito
        {
            get => _enTransito;
            set { _enTransito = value; OnPropertyChanged(nameof(EnTransito)); }
        }

        private int _entregadas;
        public int Entregadas
        {
            get => _entregadas;
            set { _entregadas = value; OnPropertyChanged(nameof(Entregadas)); }
        }

        // AGREGADO: Propiedad para búsqueda
        private string _textoBusqueda = string.Empty;
        public string TextoBusqueda
        {
            get => _textoBusqueda;
            set
            {
                _textoBusqueda = value;
                OnPropertyChanged(nameof(TextoBusqueda));
                FiltrarVentas();
            }
        }

        private Venta _ventaActual;
        public Venta VentaActual
        {
            get => _ventaActual;
            set { _ventaActual = value; OnPropertyChanged(nameof(VentaActual)); }
        }

        private ItemInventario _productoSeleccionado;
        public ItemInventario ProductoSeleccionado
        {
            get => _productoSeleccionado;
            set { _productoSeleccionado = value; OnPropertyChanged(nameof(ProductoSeleccionado)); }
        }

        private int _cantidadProducto = 1;
        public int CantidadProducto
        {
            get => _cantidadProducto;
            set { _cantidadProducto = value; OnPropertyChanged(nameof(CantidadProducto)); }
        }

        private decimal _totalVenta;
        public decimal TotalVenta
        {
            get => _totalVenta;
            set { _totalVenta = value; OnPropertyChanged(nameof(TotalVenta)); }
        }

        // Comandos
        public ICommand LimpiarVentaCommand { get; }
        public ICommand FinalizarVentaCommand { get; }
        public ICommand AgregarProductoCommand { get; }
        public ICommand EliminarDetalleCommand { get; }
        public ICommand AbrirModalCommand { get; }
        public ICommand CerrarModalCommand { get; }
        public ICommand RegistrarCommand { get; }

        // AGREGADO: Métodos para controlar el modal
        private void AbrirModal(object parameter)
        {
            VentaActual = new Venta
            {
                Fecha = DateTime.Now,
                Estado = "Pendiente"
            };
            CantidadProducto = 1;
            MostrarModal = true;
        }

        private void CerrarModal(object parameter)
        {
            MostrarModal = false;
            VentaActual = new Venta { Fecha = DateTime.Now };
            CantidadProducto = 1;
        }

        private bool ValidarVentaModal()
        {
            if (string.IsNullOrWhiteSpace(VentaActual.Cliente))
            {
                MessageBox.Show("Debe ingresar quien recibe", "Validación",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (string.IsNullOrWhiteSpace(VentaActual.TipoVenta))
            {
                MessageBox.Show("Debe seleccionar el tipo de venta", "Validación",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (string.IsNullOrWhiteSpace(VentaActual.Estado))
            {
                MessageBox.Show("Debe seleccionar el estado", "Validación",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (CantidadProducto <= 0)
            {
                MessageBox.Show("La cantidad debe ser mayor a 0", "Validación",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            return true;
        }

        // AGREGADO: Método para actualizar estadísticas
        private void ActualizarEstadisticas()
        {
            TotalEntregas = ventasDatabase.ObtenerTotalEntregas();
            Pendientes = ventasDatabase.ObtenerPorEstado("Pendiente");
            EnTransito = ventasDatabase.ObtenerPorEstado("En Tránsito");
            Entregadas = ventasDatabase.ObtenerPorEstado("Entregado");
        }

        // AGREGADO: Método para filtrar ventas
        private void FiltrarVentas()
        {
            var todasVentas = ventasDatabase.ObtenerTodasVentas();

            if (string.IsNullOrWhiteSpace(TextoBusqueda))
            {
                ListaVentas.Clear();
                foreach (var venta in todasVentas)
                {
                    ListaVentas.Add(venta);
                }
            }
            else
            {
                var filtradas = todasVentas.Where(v =>
                    v.Cliente.Contains(TextoBusqueda, StringComparison.OrdinalIgnoreCase) ||
                    v.TipoVenta.Contains(TextoBusqueda, StringComparison.OrdinalIgnoreCase) ||
                    v.Estado.Contains(TextoBusqueda, StringComparison.OrdinalIgnoreCase)
                ).ToList();

                ListaVentas.Clear();
                foreach (var venta in filtradas)
                {
                    ListaVentas.Add(venta);
                }
            }
        }

        // Métodos existentes...
        private void CargarVentas()
        {
            ListaVentas.Clear();
            var ventas = ventasDatabase.ObtenerTodasVentas();

            foreach (var venta in ventas)
            {
                ListaVentas.Add(venta);
            }
        }

        private void CargarProductosDisponibles()
        {
            ProductosDisponibles.Clear();
            var productos = inventarioDatabase.ObtenerTodosItems()
                .Where(p => p.CantidadStock > 0)
                .ToList();

            foreach (var producto in productos)
            {
                ProductosDisponibles.Add(producto);
            }
        }

        private void AgregarProducto(object parameter)
        {
            if (ProductoSeleccionado == null)
            {
                MessageBox.Show("Debe seleccionar un producto", "Validación",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (CantidadProducto <= 0)
            {
                MessageBox.Show("La cantidad debe ser mayor a 0", "Validación",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (CantidadProducto > ProductoSeleccionado.CantidadStock)
            {
                MessageBox.Show($"Stock insuficiente. Disponible: {ProductoSeleccionado.CantidadStock}",
                    "Validación", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var detalle = new DetalleVenta
            {
                IdItem = ProductoSeleccionado.IdItem,
                NombreProducto = ProductoSeleccionado.Nombre,
                Cantidad = CantidadProducto,
                PrecioUnitario = ProductoSeleccionado.CostoUnitario,
                Subtotal = CantidadProducto * ProductoSeleccionado.CostoUnitario
            };

            DetalleVenta.Add(detalle);
            CalcularTotal();
            CantidadProducto = 1;
        }

        private void EliminarDetalle(object parameter)
        {
            if (parameter is DetalleVenta detalle)
            {
                DetalleVenta.Remove(detalle);
                CalcularTotal();
            }
        }

        private void CalcularTotal()
        {
            TotalVenta = DetalleVenta.Sum(d => d.Subtotal);
            VentaActual.CostoTotal = TotalVenta;
        }

        private void LimpiarVenta(object parameter)
        {
            VentaActual = new Venta { Fecha = DateTime.Now };
            DetalleVenta.Clear();
            TotalVenta = 0;
            CantidadProducto = 1;
            ProductoSeleccionado = null;
        }

        private void FinalizarVenta(object parameter)
        {
            if (ValidarVenta())
            {
                VentaActual.CostoTotal = TotalVenta;

                var detalles = DetalleVenta.ToList();

                if (ventasDatabase.InsertarVenta(VentaActual))
                {
                    MessageBox.Show($"Venta registrada exitosamente\n\nTotal: {TotalVenta:C}",
                        "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);

                    CargarVentas();
                    CargarProductosDisponibles();
                    ActualizarEstadisticas();
                    LimpiarVenta(null);
                }
            }
        }

        private bool ValidarVenta()
        {
            if (string.IsNullOrWhiteSpace(VentaActual.Cliente))
            {
                MessageBox.Show("Debe ingresar el nombre del cliente", "Validación",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (DetalleVenta.Count == 0)
            {
                MessageBox.Show("Debe agregar al menos un producto", "Validación",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (string.IsNullOrWhiteSpace(VentaActual.MetodoPago))
            {
                MessageBox.Show("Debe seleccionar un método de pago", "Validación",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            return true;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private void EditarVenta(object parameter)
        {
            if (parameter is Venta venta)
            {
                // Cargar los datos de la venta en el modal
                VentaActual = new Venta
                {
                    IdVenta = venta.IdVenta,
                    Fecha = venta.Fecha,
                    Cliente = venta.Cliente,
                    TipoVenta = venta.TipoVenta,
                    Categoria = venta.Categoria,
                    Cantidad = venta.Cantidad,
                    CostoTotal = venta.CostoTotal,
                    Estado = venta.Estado,
                    Observaciones = venta.Observaciones
                };

                CantidadProducto = venta.Cantidad;
                MostrarModal = true;
            }
        }

        private void EliminarVenta(object parameter)
        {
            if (parameter is Venta venta)
            {
                var resultado = MessageBox.Show(
                    $"¿Está seguro de eliminar la venta de {venta.Cliente}?\n\nEsta acción no se puede deshacer.",
                    "Confirmar eliminación",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (resultado == MessageBoxResult.Yes)
                {
                    if (ventasDatabase.EliminarVenta(venta.IdVenta))
                    {
                        MessageBox.Show("Venta eliminada exitosamente", "Éxito",
                            MessageBoxButton.OK, MessageBoxImage.Information);

                        CargarVentas();
                        ActualizarEstadisticas();
                    }
                    else
                    {
                        MessageBox.Show("Error al eliminar la venta", "Error",
                            MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

        // Actualizar el método RegistrarVenta para manejar edición
        private void RegistrarVenta(object parameter)
        {
            if (ValidarVentaModal())
            {
                VentaActual.Cantidad = CantidadProducto;

                bool resultado;
                string mensaje;

                // Si tiene ID, es una edición
                if (VentaActual.IdVenta > 0)
                {
                    resultado = ventasDatabase.ActualizarVenta(VentaActual);
                    mensaje = resultado ? "Entrega actualizada exitosamente" : "Error al actualizar";
                }
                else
                {
                    resultado = ventasDatabase.InsertarVenta(VentaActual);
                    mensaje = resultado ? "Entrega registrada exitosamente" : "Error al registrar";
                }

                if (resultado)
                {
                    MessageBox.Show(mensaje, "Éxito",
                        MessageBoxButton.OK, MessageBoxImage.Information);

                    CargarVentas();
                    ActualizarEstadisticas();
                    CerrarModal(null);
                }
                else
                {
                    MessageBox.Show(mensaje, "Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

    }
}