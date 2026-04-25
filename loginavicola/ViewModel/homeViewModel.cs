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

        private SeriesCollection _estadoAvesSeries;
        public SeriesCollection EstadoAvesSeries { get => _estadoAvesSeries; set { _estadoAvesSeries = value; OnPropertyChanged(); } }

        private SeriesCollection _produccionCategoriaSeries;
        public SeriesCollection ProduccionCategoriaSeries { get => _produccionCategoriaSeries; set { _produccionCategoriaSeries = value; OnPropertyChanged(); } }

        private string[] _etiquetasDias = Array.Empty<string>();
        public string[] EtiquetasDias { get => _etiquetasDias; set { _etiquetasDias = value; OnPropertyChanged(); } }

        // NUEVA PROPIEDAD PARA LA GRÁFICA DE CONSUMO LINEAL
        private ChartValues<double> _valoresConsumoAlimento;
        public ChartValues<double> ValoresConsumoAlimento
        {
            get => _valoresConsumoAlimento;
            set { _valoresConsumoAlimento = value; OnPropertyChanged(); }
        }

        public homeViewModel()
        {
            // Inicializar con valores vacíos
            ValoresConsumoAlimento = new ChartValues<double>();
            EtiquetasDias = new string[0];

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

            CargarGraficaSalud();
            CargarGraficaProduccion();
            CargarGraficaConsumo(); // NUEVO: Cargar gráfica de consumo

            var listaItems = inventarioDb.ObtenerTodosItems();
            TotalAlimento = listaItems?.Where(i => i.Categoria != null && i.Categoria.ToLower().Contains("alimento"))
                            .Sum(i => (decimal)i.CantidadStock) ?? 0;
        }

        private void CargarGraficaSalud()
        {
            var listaDiagnosticos = diagDb.ObtenerTodosDiagnosticos();
            var series = new SeriesCollection();
            int afectados = 0;

            if (listaDiagnosticos != null)
            {
                var grupos = listaDiagnosticos.Where(d => d.Estado == "Activo")
                    .GroupBy(d => d.Tipo)
                    .Select(g => new { Tipo = g.Key, Cantidad = g.Sum(d => d.GallinasAfectadas) });

                foreach (var g in grupos)
                {
                    series.Add(new PieSeries { Title = g.Tipo, Values = new ChartValues<int> { g.Cantidad }, DataLabels = true });
                    afectados += g.Cantidad;
                }
            }

            int sanas = Math.Max(0, TotalAves - afectados);
            series.Add(new PieSeries { Title = "Sanas", Values = new ChartValues<int> { sanas }, Fill = Brushes.MediumSeaGreen, DataLabels = true });
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

        // NUEVO MÉTODO: Cargar gráfica de consumo directamente en el ViewModel
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