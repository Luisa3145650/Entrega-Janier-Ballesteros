using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using LiveCharts;
using LiveCharts.Wpf;
using loginavicola.ViewModel;

namespace loginavicola.View
{
    public partial class homeView : UserControl
    {
        private DispatcherTimer _carruselTimer;
        private int _indiceActual = 0;
        private homeViewModel _viewModel;

        public homeView()
        {
            InitializeComponent();
            _viewModel = new homeViewModel();
            this.DataContext = _viewModel;
            IniciarCarrusel();
        }

        private void IniciarCarrusel()
        {
            _carruselTimer = new DispatcherTimer();
            _carruselTimer.Interval = TimeSpan.FromSeconds(3); // Cambia cada 3 segundos
            _carruselTimer.Tick += (s, e) => SiguienteTarjeta();
            _carruselTimer.Start();
        }

        private void SiguienteTarjeta()
        {
            _indiceActual = (_indiceActual + 1) % 4;
            MostrarTarjetaActual();
        }

        private void MostrarTarjetaActual()
        {
            CarruselContent.Children.Clear();

            switch (_indiceActual)
            {
                case 0:
                    MostrarGraficaProduccionHuevos();
                    break;
                case 1:
                    MostrarGraficaProduccionCategoria();
                    break;
                case 2:
                    MostrarGraficaEstadoAves();
                    break;
                case 3:
                    MostrarGraficaConsumoAlimento();
                    break;
            }

            ActualizarIndicadores();
        }

        private void MostrarGraficaProduccionHuevos()
        {
            var grid = new Grid();

            var titulo = new TextBlock
            {
                Text = "🥚 Producción de huevos - Última semana",
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Foreground = (SolidColorBrush)new BrushConverter().ConvertFrom("#1F2937"),
                Margin = new Thickness(0, 0, 0, 15)
            };

            var chart = new CartesianChart
            {
                Height = 300
            };

            // Datos de ejemplo (puedes cambiarlos por datos reales de tu BD)
            var series = new ColumnSeries
            {
                Title = "Huevos",
                Values = new ChartValues<int> { 800, 850, 920, 880, 900, 870, 890 },
                Fill = (SolidColorBrush)new BrushConverter().ConvertFrom("#10B981"),
                MaxColumnWidth = 45
            };
            chart.Series.Add(series);

            var axisX = new Axis
            {
                Labels = new[] { "Lun", "Mar", "Mié", "Jue", "Vie", "Sáb", "Dom" },
                FontSize = 11
            };
            chart.AxisX.Add(axisX);

            var axisY = new Axis
            {
                FontSize = 11,
                Title = "Cantidad de huevos"
            };
            chart.AxisY.Add(axisY);

            grid.Children.Add(titulo);
            grid.Children.Add(chart);

            Grid.SetRow(titulo, 0);
            Grid.SetRow(chart, 1);

            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            CarruselContent.Children.Add(grid);
        }

        private void MostrarGraficaProduccionCategoria()
        {
            var grid = new Grid();

            var titulo = new TextBlock
            {
                Text = "📊 Producción por categoría",
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Foreground = (SolidColorBrush)new BrushConverter().ConvertFrom("#1F2937"),
                Margin = new Thickness(0, 0, 0, 15)
            };

            var chart = new PieChart
            {
                Series = _viewModel.ProduccionCategoriaSeries,
                LegendLocation = LegendLocation.Bottom,
                InnerRadius = 0,
                Height = 300
            };

            grid.Children.Add(titulo);
            grid.Children.Add(chart);

            Grid.SetRow(titulo, 0);
            Grid.SetRow(chart, 1);

            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            CarruselContent.Children.Add(grid);
        }

        private void MostrarGraficaEstadoAves()
        {
            var grid = new Grid();

            var titulo = new TextBlock
            {
                Text = "🐔 Estado de salud de las aves",
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Foreground = (SolidColorBrush)new BrushConverter().ConvertFrom("#1F2937"),
                Margin = new Thickness(0, 0, 0, 15)
            };

            var chart = new PieChart
            {
                Series = _viewModel.EstadoAvesSeries,
                LegendLocation = LegendLocation.Bottom,
                InnerRadius = 60,
                Height = 300
            };

            grid.Children.Add(titulo);
            grid.Children.Add(chart);

            Grid.SetRow(titulo, 0);
            Grid.SetRow(chart, 1);

            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            CarruselContent.Children.Add(grid);
        }

        private void MostrarGraficaConsumoAlimento()
        {
            var grid = new Grid();

            var titulo = new TextBlock
            {
                Text = "🌾 Consumo de alimento (últimos 7 días)",
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Foreground = (SolidColorBrush)new BrushConverter().ConvertFrom("#1F2937"),
                Margin = new Thickness(0, 0, 0, 15)
            };

            // Cargar datos reales de consumo
            var consumos = _viewModel.ObtenerConsumosRecientes(7);

            var chart = new CartesianChart
            {
                Height = 300
            };

            if (consumos != null && consumos.Any())
            {
                _viewModel.EtiquetasDias = consumos.Select(c => c.FechaConsumo.ToString("dd/MMM")).ToArray();

                var series = new LineSeries
                {
                    Title = "Consumo (kg)",
                    Values = new ChartValues<double>(consumos.Select(c => (double)c.CantidadConsumida)),
                    Stroke = (SolidColorBrush)new BrushConverter().ConvertFrom("#9C59B6"),
                    PointGeometrySize = 8,
                    PointForeground = Brushes.White,
                    Fill = Brushes.Transparent
                };
                chart.Series.Add(series);

                var axisX = new Axis
                {
                    Labels = _viewModel.EtiquetasDias,
                    FontSize = 11
                };
                chart.AxisX.Add(axisX);
            }
            else
            {
                // Datos de ejemplo si no hay datos en la BD
                var series = new LineSeries
                {
                    Title = "Consumo (kg)",
                    Values = new ChartValues<double> { 120, 135, 128, 142, 138, 145, 140 },
                    Stroke = (SolidColorBrush)new BrushConverter().ConvertFrom("#9C59B6"),
                    PointGeometrySize = 8,
                    PointForeground = Brushes.White,
                    Fill = Brushes.Transparent
                };
                chart.Series.Add(series);

                var axisX = new Axis
                {
                    Labels = new[] { "Lun", "Mar", "Mié", "Jue", "Vie", "Sáb", "Dom" },
                    FontSize = 11
                };
                chart.AxisX.Add(axisX);
            }

            var axisY = new Axis
            {
                FontSize = 11,
                Title = "Kilogramos"
            };
            chart.AxisY.Add(axisY);

            grid.Children.Add(titulo);
            grid.Children.Add(chart);

            Grid.SetRow(titulo, 0);
            Grid.SetRow(chart, 1);

            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            CarruselContent.Children.Add(grid);
        }

        private void ActualizarIndicadores()
        {
            var indicadores = new[] { Indicator0, Indicator1, Indicator2, Indicator3 };
            var colores = new[] { "#D1D5DB", "#D1D5DB", "#D1D5DB", "#D1D5DB" };
            colores[_indiceActual] = "#10B981";

            for (int i = 0; i < indicadores.Length; i++)
            {
                if (indicadores[i] != null)
                {
                    indicadores[i].Background = (SolidColorBrush)new BrushConverter().ConvertFrom(colores[i]);
                }
            }
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            // Actualizar datos
            _viewModel.ActualizarCards();

            // Mostrar primera tarjeta
            MostrarTarjetaActual();

            // Configurar eventos de clic en indicadores
            Indicator0.MouseLeftButtonUp += (s, args) => { _carruselTimer.Stop(); _indiceActual = 0; MostrarTarjetaActual(); _carruselTimer.Start(); };
            Indicator1.MouseLeftButtonUp += (s, args) => { _carruselTimer.Stop(); _indiceActual = 1; MostrarTarjetaActual(); _carruselTimer.Start(); };
            Indicator2.MouseLeftButtonUp += (s, args) => { _carruselTimer.Stop(); _indiceActual = 2; MostrarTarjetaActual(); _carruselTimer.Start(); };
            Indicator3.MouseLeftButtonUp += (s, args) => { _carruselTimer.Stop(); _indiceActual = 3; MostrarTarjetaActual(); _carruselTimer.Start(); };
        }
    }
}