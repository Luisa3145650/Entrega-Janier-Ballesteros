using LiveCharts;
using LiveCharts.Wpf;
using loginavicola.ViewModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Separator = LiveCharts.Wpf.Separator;

namespace loginavicola.View
{
    public partial class homeView : UserControl  // ← quitado INotifyPropertyChanged
    {
        public homeView()
        {
            InitializeComponent();
            this.DataContext = new homeViewModel();
            CargarGraficaConsumoAlimento();
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is homeViewModel vm)
            {
                vm.ActualizarCards();
                CargarGraficaConsumoAlimento();
            }
        }

        private void CargarGraficaConsumoAlimento()
        {
            if (DataContext is not homeViewModel vm)
                return;

            var consumos = vm.ObtenerConsumosRecientes(7);

            if (!consumos.Any())
            {
                vm.EtiquetasDias = new[] { "Sin datos" }; // ← escribe en el VM
                ConsumoAlimentoChart.Series = new SeriesCollection();
                return;
            }

            vm.EtiquetasDias = consumos               // ← escribe en el VM
                .Select(c => c.FechaConsumo.ToString("dd/MMM"))
                .ToArray();

            ConsumoAlimentoChart.Series = new SeriesCollection
            {
                new LineSeries
                {
                    Title = "Consumo Semanal (kg)",
                    Values = new ChartValues<double>(
                        consumos.Select(c => (double)c.CantidadConsumida)
                    ),
                    PointGeometrySize = 10,
                    Stroke = Brushes.MediumPurple,
                    Fill   = Brushes.Transparent
                }
            };
        }
    }
}