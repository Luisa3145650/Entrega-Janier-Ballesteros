using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using LiveCharts;
using LiveCharts.Wpf;
using loginavicola.ViewModel;

namespace loginavicola.View
{
    public partial class homeView : UserControl
    {
        public homeView()
        {
            InitializeComponent();
            this.DataContext = new homeViewModel();
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is homeViewModel vm)
            {
                // 1. Actualiza todos los datos (Cards y Gráficas de Pastel)
                vm.ActualizarCards();

                // 2. Vinculamos las gráficas de pastel manualmente si no usas Binding en XAML
                if (CategoriaChart != null)
                    CategoriaChart.Series = vm.ProduccionCategoriaSeries;

                if (EstadoAvesChart != null)
                    EstadoAvesChart.Series = vm.EstadoAvesSeries;

                // 3. Cargamos la gráfica de líneas de consumo
                CargarGraficaConsumo(vm);
            }
        }

        private void CargarGraficaConsumo(homeViewModel vm)
        {
            var consumos = vm.ObtenerConsumosRecientes(7);

            if (consumos == null || !consumos.Any())
            {
                ConsumoAlimentoChart.Series = new SeriesCollection();
                return;
            }

            vm.EtiquetasDias = consumos.Select(c => c.FechaConsumo.ToString("dd/MMM")).ToArray();

            ConsumoAlimentoChart.Series = new SeriesCollection
            {
                new LineSeries
                {
                    Title = "Consumo (kg)",
                    Values = new ChartValues<double>(consumos.Select(c => (double)c.CantidadConsumida)),
                    Stroke = Brushes.MediumPurple,
                    PointGeometrySize = 8,
                    PointForeground = Brushes.White,
                    Fill = new LinearGradientBrush
                    {
                        StartPoint = new Point(0, 0),
                        EndPoint = new Point(0, 1),
                        GradientStops = new GradientStopCollection
                        {
                            new GradientStop(Color.FromArgb(50, 147, 112, 219), 0),
                            new GradientStop(Colors.Transparent, 1)
                        }
                    }
                }
            };
        }
    }
}