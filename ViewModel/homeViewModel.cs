using loginavicola.Database;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using LiveCharts;
using LiveCharts.Wpf;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using loginavicola.Model;
using System.Windows.Threading;

namespace loginavicola.ViewModel
{
    public class homeViewModel : INotifyPropertyChanged
    {
        private readonly ConsumoDatabase _consumoDatabase = new ConsumoDatabase();
        private readonly LoteDatabase database = new LoteDatabase();
        private readonly InventarioDatabase inventarioDb = new InventarioDatabase();
        private readonly DiagnosticoDatabase diagDb = new DiagnosticoDatabase();
        private readonly ClasificacionProduccionDatabase _produccionDb = new ClasificacionProduccionDatabase();

        private DispatcherTimer _timer;

        // PROPIEDADES NOTIFICABLES
        private int _totalLotes;
        public int TotalLotes { get => _totalLotes; set { _totalLotes = value; OnPropertyChanged(); } }

        private int _totalAves;
        public int TotalAves { get => _totalAves; set { _totalAves = value; OnPropertyChanged(); } }

        private decimal _totalAlimento;
        public decimal TotalAlimento { get => _totalAlimento; set { _totalAlimento = value; OnPropertyChanged(); } }

        private int _totalHuevosHoy;
        public int TotalHuevosHoy { get => _totalHuevosHoy; set { _totalHuevosHoy = value; OnPropertyChanged(); } }

        private int _totalAlertasStock;
        public int TotalAlertasStock { get => _totalAlertasStock; set { _totalAlertasStock = value; OnPropertyChanged(); } }

        public System.Windows.Input.ICommand FiltrarAlertasCommand { get; }

        private SeriesCollection _estadoAvesSeries;
        public SeriesCollection EstadoAvesSeries { get => _estadoAvesSeries; set { _estadoAvesSeries = value; OnPropertyChanged(); } }

        private SeriesCollection _produccionCategoriaSeries;
        public SeriesCollection ProduccionCategoriaSeries { get => _produccionCategoriaSeries; set { _produccionCategoriaSeries = value; OnPropertyChanged(); } }

        private string[] _etiquetasDias = Array.Empty<string>();
        public string[] EtiquetasDias { get => _etiquetasDias; set { _etiquetasDias = value; OnPropertyChanged(); } }

        // PROPIEDADES PARA LA GRÁFICA DE CONSUMO
        private ChartValues<double> _valoresConsumoAlimento;
        public ChartValues<double> ValoresConsumoAlimento
        {
            get => _valoresConsumoAlimento;
            set { _valoresConsumoAlimento = value; OnPropertyChanged(); }
        }

        // PROPIEDADES PARA LA GRÁFICA DE PRODUCCIÓN SEMANAL
        private ChartValues<int> _valoresProduccionSemanal;
        public ChartValues<int> ValoresProduccionSemanal
        {
            get => _valoresProduccionSemanal;
            set { _valoresProduccionSemanal = value; OnPropertyChanged(); }
        }

        private string[] _etiquetasDiasSemana = new[] { "Lun", "Mar", "Mié", "Jue", "Vie", "Sáb", "Dom" };
        public string[] EtiquetasDiasSemana
        {
            get => _etiquetasDiasSemana;
            set { _etiquetasDiasSemana = value; OnPropertyChanged(); }
        }

        public homeViewModel()
        {
            // Inicializar con valores vacíos
            ValoresConsumoAlimento = new ChartValues<double>();
            ValoresProduccionSemanal = new ChartValues<int>();
            EtiquetasDias = new string[0];

            FiltrarAlertasCommand = new Helpers.RelayCommand(_ =>
            {
                var lowStockItems = inventarioDb.ObtenerTodosItems()
                    .Where(i => i.CantidadStock <= i.StockMinimo)
                    .ToList();

                if (lowStockItems.Any())
                {
                    string detalle = string.Join("\n", lowStockItems.Select(i => $"• {i.Nombre} (Stock Actual: {i.CantidadStock}, Mínimo: {i.StockMinimo})"));
                    System.Windows.MessageBox.Show($"⚠️ Productos con Stock Crítico en Inventario:\n\n{detalle}", "Alertas de Stock Crítico", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                }
                else
                {
                    System.Windows.MessageBox.Show("✅ No hay alertas de stock bajo. Todos los productos superan el stock mínimo.", "Inventario Óptimo", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                }
            });

            ActualizarCards();
            IniciarTimer();
        }

        private void IniciarTimer()
        {
            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(10) };
            _timer.Tick += (s, e) => ActualizarCards();
            _timer.Start();
        }

        public void ActualizarCards()
        {
            TotalLotes = database.ObtenerTotalLotes();
            TotalAves = database.ObtenerTotalAves();
            TotalHuevosHoy = _produccionDb.ObtenerProduccionHoy();
            TotalAlertasStock = inventarioDb.ObtenerStockBajo();

            CargarGraficaSalud();
            CargarGraficaProduccion();
            CargarGraficaConsumo();
            CargarProduccionSemanal();

            var listaItems = inventarioDb.ObtenerTodosItems();
            TotalAlimento = listaItems?.Where(i => i.Categoria != null && i.Categoria.ToLower().Contains("alimento"))
                            .Sum(i => (decimal)i.CantidadStock) ?? 0;
        }

        private void CargarGraficaSalud()
        {
            // Obtener aves en producción (Estado = Activo)
            int avesEnProduccion = database.ObtenerTotalAvesEnProduccion();

            // Obtener aves que terminaron su etapa (Estado = Pensionado)
            int avesPensionadas = database.ObtenerTotalAvesPensionadas();

            var series = new SeriesCollection();

            // Agregar aves en producción
            series.Add(new PieSeries
            {
                Title = "En producción",
                Values = new ChartValues<int> { avesEnProduccion },
                Fill = Brushes.MediumSeaGreen,
                DataLabels = true
            });

            // Agregar aves pensionadas
            series.Add(new PieSeries
            {
                Title = "Pensionadas",
                Values = new ChartValues<int> { avesPensionadas },
                Fill = Brushes.OrangeRed,
                DataLabels = true
            });

            EstadoAvesSeries = series;
        }

        private void CargarGraficaProduccion()
        {
            var datos = _produccionDb.ObtenerProduccionPorCategorias();
            var series = new SeriesCollection();

            if (datos != null)
            {
                foreach (var item in datos)
                {
                    series.Add(new PieSeries { Title = item.Categoria, Values = new ChartValues<int> { item.Cantidad }, DataLabels = true });
                }
            }
            ProduccionCategoriaSeries = series;
        }

        private void CargarGraficaConsumo()
        {
            var consumos = ObtenerConsumosRecientes(7);

            if (consumos == null || !consumos.Any())
            {
                ValoresConsumoAlimento = new ChartValues<double>();
                EtiquetasDias = new string[0];
                return;
            }

            EtiquetasDias = consumos.Select(c => c.FechaConsumo.ToString("dd/MMM")).ToArray();
            ValoresConsumoAlimento = new ChartValues<double>(consumos.Select(c => (double)c.CantidadConsumida));
        }

        private void CargarProduccionSemanal()
        {
            try
            {
                var produccionSemanal = _produccionDb.ObtenerProduccionUltimos7Dias();

                if (produccionSemanal != null && produccionSemanal.Any())
                {
                    var valores = new ChartValues<int>();
                    var etiquetas = new List<string>();

                    // Ordenar por día de la semana
                    var ordenados = produccionSemanal.OrderBy(p => p.DiaSemanaNum).ToList();

                    foreach (var item in ordenados)
                    {
                        valores.Add(item.Cantidad);
                        etiquetas.Add(item.DiaSemana);
                    }

                    ValoresProduccionSemanal = valores;
                    EtiquetasDiasSemana = etiquetas.ToArray();
                }
                else
                {
                    // Datos de ejemplo si no hay datos en la BD
                    ValoresProduccionSemanal = new ChartValues<int> { 0, 0, 0, 0, 0, 0, 0 };
                    EtiquetasDiasSemana = new[] { "Lun", "Mar", "Mié", "Jue", "Vie", "Sáb", "Dom" };
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error al cargar producción semanal: {ex.Message}");
                ValoresProduccionSemanal = new ChartValues<int> { 0, 0, 0, 0, 0, 0, 0 };
            }
        }

        public List<Consumo> ObtenerConsumosRecientes(int cantidad = 7)
        {
            return _consumoDatabase.ObtenerConsumos()
                .OrderByDescending(c => c.FechaConsumo)
                .Take(cantidad)
                .OrderBy(c => c.FechaConsumo)
                .ToList();
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}