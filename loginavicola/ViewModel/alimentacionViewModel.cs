using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows.Input;
using loginavicola.Model;
using loginavicola.Helpers; // <-- Usa el RelayCommand original de aquí
using System.Collections.Generic;
using loginavicola.Database;
using System.Windows;

using ModelAlimento = loginavicola.Model.Alimento;
using ModelConsumo = loginavicola.Model.Consumo;
using ModelLoteGallina = loginavicola.Model.LoteGallina;

namespace loginavicola.ViewModel
{
    public class AlimentacionViewModel : INotifyPropertyChanged
    {
        private readonly ConsumoDatabase database;
        private readonly InventarioDatabase inventarioDatabase;

        private ModelConsumo _consumoActual = new ModelConsumo
        {
            FechaConsumo = DateTime.Now,
            UnidadMedida = "kg",
            Turno = "Semanal"
        };

        private string _textoBusqueda = string.Empty;
        private decimal _consumoDia;
        private decimal _consumoSemanal;
        private decimal _alimentoDisponible;

        private int _elementosPorPagina = 10;
        private int _paginaActual = 1;
        private int _totalPaginas = 1;

        private ModelAlimento? _alimentoSeleccionado;
        public ModelAlimento? AlimentoSeleccionado
        {
            get => _alimentoSeleccionado;
            set
            {
                _alimentoSeleccionado = value;
                OnPropertyChanged(nameof(AlimentoSeleccionado));

                if (value != null && ConsumoActual != null)
                {
                    ConsumoActual.IdAlimento = value.IdAlimento;
                    OnPropertyChanged(nameof(ConsumoActual));
                }
            }
        }

        public AlimentacionViewModel()
        {
            database = new ConsumoDatabase();
            inventarioDatabase = new InventarioDatabase();

            Consumos = new ObservableCollection<ModelConsumo>();
            Alimentos = new ObservableCollection<ModelAlimento>();
            LotesActivos = new ObservableCollection<ModelLoteGallina>();
            UnidadesMedida = new ObservableCollection<string> { "kg" };
            Turnos = new ObservableCollection<string> { "Semanal" };

            // Instanciación usando los comandos de tus Helpers globales
            PaginaAnteriorCommand = new RelayCommand(param => { PaginaActual--; }, param => PaginaActual > 1);
            PaginaSiguienteCommand = new RelayCommand(param => { PaginaActual++; }, param => PaginaActual < TotalPaginas);

            CargarDatos();
        }

        public int ElementosPorPagina
        {
            get => _elementosPorPagina;
            set
            {
                if (_elementosPorPagina != value)
                {
                    _elementosPorPagina = value;
                    OnPropertyChanged(nameof(ElementosPorPagina));
                    _paginaActual = 1;
                    OnPropertyChanged(nameof(PaginaActual));
                    CargarConsumos();
                }
            }
        }

        public int PaginaActual
        {
            get => _paginaActual;
            set
            {
                if (_paginaActual != value)
                {
                    _paginaActual = value;
                    OnPropertyChanged(nameof(PaginaActual));
                    CargarConsumos();
                }
            }
        }

        public int TotalPaginas
        {
            get => _totalPaginas;
            set { _totalPaginas = value; OnPropertyChanged(nameof(TotalPaginas)); }
        }

        public ICommand PaginaAnteriorCommand { get; }
        public ICommand PaginaSiguienteCommand { get; }

        public decimal ConsumoDia
        {
            get => _consumoDia;
            set { _consumoDia = value; OnPropertyChanged(nameof(ConsumoDia)); }
        }

        public decimal ConsumoSemanal
        {
            get => _consumoSemanal;
            set { _consumoSemanal = value; OnPropertyChanged(nameof(ConsumoSemanal)); }
        }

        public decimal AlimentoDisponible
        {
            get => _alimentoDisponible;
            set { _alimentoDisponible = value; OnPropertyChanged(nameof(AlimentoDisponible)); }
        }

        public ObservableCollection<ModelConsumo> Consumos { get; set; }
        public ObservableCollection<ModelAlimento> Alimentos { get; set; }
        public ObservableCollection<ModelLoteGallina> LotesActivos { get; set; }
        public ObservableCollection<string> UnidadesMedida { get; set; }
        public ObservableCollection<string> Turnos { get; set; }

        public ModelConsumo ConsumoActual
        {
            get => _consumoActual;
            set { _consumoActual = value; OnPropertyChanged(nameof(ConsumoActual)); }
        }

        public string TextoBusqueda
        {
            get => _textoBusqueda;
            set
            {
                _textoBusqueda = value;
                OnPropertyChanged(nameof(TextoBusqueda));
                _paginaActual = 1;
                OnPropertyChanged(nameof(PaginaActual));
                FiltrarConsumos();
            }
        }

        public void CargarDatos()
        {
            CargarConsumos();
            CargarAlimentos();
            CargarLotes();
            ActualizarEstadisticas();
        }

        private void CargarConsumos()
        {
            Consumos.Clear();
            var todosConsumos = database.ObtenerConsumos();

            if (todosConsumos != null && todosConsumos.Any())
            {
                TotalPaginas = (int)Math.Ceiling((double)todosConsumos.Count / ElementosPorPagina);
                if (TotalPaginas < 1) TotalPaginas = 1;

                int omitirRegistros = (PaginaActual - 1) * ElementosPorPagina;
                var consumosPaginados = todosConsumos.Skip(omitirRegistros).Take(ElementosPorPagina).ToList();

                foreach (var consumo in consumosPaginados)
                    Consumos.Add(consumo);
            }
            else
            {
                TotalPaginas = 1;
            }
        }

        private void CargarAlimentos()
        {
            Alimentos.Clear();
            var itemsInventario = inventarioDatabase.ObtenerTodosItems()
                .Where(i => i.Categoria.ToLower().Contains("alimento") && i.CantidadStock > 0)
                .ToList();

            if (itemsInventario.Count == 0)
            {
                var todosItems = inventarioDatabase.ObtenerTodosItems();
                string detalleItems = todosItems.Any()
                    ? string.Join("\n", todosItems.Select(i => $"• {i.Nombre} | Categoría: '{i.Categoria}' | Stock: {i.CantidadStock}"))
                    : "No hay productos registrados en inventario.";

                MessageBox.Show(
                    $"No se encontraron alimentos disponibles.\n\nProductos en inventario:\n{detalleItems}",
                    "Sin Alimentos Disponibles", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            foreach (var item in itemsInventario)
            {
                Alimentos.Add(new ModelAlimento
                {
                    IdAlimento = item.IdItem,
                    Nombre = item.Nombre,
                    StockDisponible = item.CantidadStock,
                    UnidadMedida = "kg"
                });
            }
        }

        private void CargarLotes()
        {
            LotesActivos.Clear();
            var lotes = database.ObtenerLotesActivos();
            foreach (var lote in lotes)
                LotesActivos.Add(lote);
        }

        private void ActualizarEstadisticas()
        {
            ConsumoDia = database.ObtenerConsumoDia();
            ConsumoSemanal = database.ObtenerConsumoSemanal();

            var totalAlimentoKg = inventarioDatabase.ObtenerTodosItems()
                .Where(i => i.Categoria.ToLower().Contains("alimento"))
                .Sum(i => (decimal)i.CantidadStock);

            AlimentoDisponible = totalAlimentoKg;
        }

        public bool GuardarConsumo()
        {
            if (ValidarConsumo())
            {
                var loteSeleccionado = LotesActivos.FirstOrDefault(l => l.IdLote == ConsumoActual.IdLoteGallinas);
                if (loteSeleccionado == null) return false;

                ConsumoActual.CantidadGallinas = loteSeleccionado.CantidadActual;

                var (consumoEsperado, merma, alertaMerma) = database.CalcularConsumo(
                    ConsumoActual.CantidadGallinas,
                    ConsumoActual.CantidadConsumida);

                ConsumoActual.ConsumoEsperado = consumoEsperado;
                ConsumoActual.Merma = merma;
                ConsumoActual.AlertaMerma = alertaMerma;

                string nombreAlimento = AlimentoSeleccionado?.Nombre ?? "Concentrado";

                if (AlimentoSeleccionado != null && ConsumoActual.CantidadConsumida > AlimentoSeleccionado.StockDisponible)
                {
                    MessageBox.Show("Stock insuficiente.", "Validación", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return false;
                }

                if (database.InsertarConsumoSemanal(ConsumoActual))
                {
                    DescontarStockInventario(ConsumoActual.IdAlimento, (int)ConsumoActual.CantidadConsumida);
                    CargarDatos();
                    LimpiarFormulario();
                    return true;
                }
            }
            return false;
        }

        private void DescontarStockInventario(int idAlimento, int cantidad)
        {
            bool descontado = inventarioDatabase.ActualizarStock(idAlimento, cantidad, "resta");
            if (!descontado)
            {
                MessageBox.Show("Error al actualizar el stock en inventario.", "Advertencia", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private bool ValidarConsumo()
        {
            if (ConsumoActual.IdLoteGallinas == 0 || AlimentoSeleccionado == null || ConsumoActual.CantidadConsumida <= 0)
                return false;
            return true;
        }

        public void LimpiarFormulario()
        {
            ConsumoActual = new ModelConsumo { FechaConsumo = DateTime.Now, UnidadMedida = "kg", Turno = "Semanal" };
            AlimentoSeleccionado = null;
        }

        private void FiltrarConsumos()
        {
            var todosConsumos = database.ObtenerConsumos();
            if (todosConsumos == null) return;

            if (string.IsNullOrWhiteSpace(TextoBusqueda))
            {
                CargarConsumos();
                return;
            }

            var consumosFiltrados = todosConsumos
                .Where(c => c.NombreAlimento.ToLower().Contains(TextoBusqueda.ToLower()) ||
                            c.IdLoteGallinas.ToString().Contains(TextoBusqueda))
                .ToList();

            Consumos.Clear();
            TotalPaginas = (int)Math.Ceiling((double)consumosFiltrados.Count / ElementosPorPagina);
            if (TotalPaginas < 1) TotalPaginas = 1;

            int omitirRegistros = (PaginaActual - 1) * ElementosPorPagina;
            var fragmentoFiltrado = consumosFiltrados.Skip(omitirRegistros).Take(ElementosPorPagina).ToList();

            foreach (var consumo in fragmentoFiltrado)
                Consumos.Add(consumo);
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}