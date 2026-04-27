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
        private bool _esNavegacionManual = false;

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
            _carruselTimer.Interval = TimeSpan.FromSeconds(5);
            _carruselTimer.Tick += (s, e) => SiguienteTarjetaAuto();
            _carruselTimer.Start();
        }

        private void SiguienteTarjetaAuto()
        {
            if (!_esNavegacionManual)
            {
                _indiceActual = (_indiceActual + 1) % 4;
                MostrarTarjetaActual();
            }
        }

        private void BtnAnterior_Click(object sender, RoutedEventArgs e)
        {
            _esNavegacionManual = true;
            _indiceActual = (_indiceActual - 1 + 4) % 4;
            MostrarTarjetaActual();
            ReiniciarTimer();
        }

        private void BtnSiguiente_Click(object sender, RoutedEventArgs e)
        {
            _esNavegacionManual = true;
            _indiceActual = (_indiceActual + 1) % 4;
            MostrarTarjetaActual();
            ReiniciarTimer();
        }

        private void ReiniciarTimer()
        {
            _carruselTimer.Stop();
            _carruselTimer.Start();

            DispatcherTimer timerReset = new DispatcherTimer();
            timerReset.Interval = TimeSpan.FromSeconds(10);
            timerReset.Tick += (s, e) => { _esNavegacionManual = false; timerReset.Stop(); };
            timerReset.Start();
        }

        private void MostrarTarjetaActual()
        {
            CarruselContent.Children.Clear();

            btnAnterior.Visibility = _indiceActual > 0 ? Visibility.Visible : Visibility.Collapsed;
            btnSiguiente.Visibility = _indiceActual < 3 ? Visibility.Visible : Visibility.Visible;

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
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            var titulo = new TextBlock
            {
                Text = "🥚 Producción de huevos - Última semana",
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Foreground = (SolidColorBrush)new BrushConverter().ConvertFrom("#1F2937"),
                Margin = new Thickness(0, 0, 0, 15)
            };

            var chart = new CartesianChart { Height = 300 };

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

            var axisY = new Axis { FontSize = 11, Title = "Cantidad de huevos" };
            chart.AxisY.Add(axisY);

            Grid.SetRow(titulo, 0);
            Grid.SetRow(chart, 1);

            grid.Children.Add(titulo);
            grid.Children.Add(chart);

            CarruselContent.Children.Add(grid);
        }

        private void MostrarGraficaProduccionCategoria()
        {
            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

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

            Grid.SetRow(titulo, 0);
            Grid.SetRow(chart, 1);

            grid.Children.Add(titulo);
            grid.Children.Add(chart);

            CarruselContent.Children.Add(grid);
        }

        private void MostrarGraficaEstadoAves()
        {
            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

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

            Grid.SetRow(titulo, 0);
            Grid.SetRow(chart, 1);

            grid.Children.Add(titulo);
            grid.Children.Add(chart);

            CarruselContent.Children.Add(grid);
        }

        private void MostrarGraficaConsumoAlimento()
        {
            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            var titulo = new TextBlock
            {
                Text = "🌾 Consumo de alimento (últimos 7 días)",
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Foreground = (SolidColorBrush)new BrushConverter().ConvertFrom("#1F2937"),
                Margin = new Thickness(0, 0, 0, 15)
            };

            var consumos = _viewModel.ObtenerConsumosRecientes(7);
            var chart = new CartesianChart { Height = 300 };

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

                var axisX = new Axis { Labels = _viewModel.EtiquetasDias, FontSize = 11 };
                chart.AxisX.Add(axisX);
            }
            else
            {
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

                var axisX = new Axis { Labels = new[] { "Lun", "Mar", "Mié", "Jue", "Vie", "Sáb", "Dom" }, FontSize = 11 };
                chart.AxisX.Add(axisX);
            }

            var axisY = new Axis { FontSize = 11, Title = "Kilogramos" };
            chart.AxisY.Add(axisY);

            Grid.SetRow(titulo, 0);
            Grid.SetRow(chart, 1);

            grid.Children.Add(titulo);
            grid.Children.Add(chart);

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

            // Configurar eventos de clic en indicadores
            for (int i = 0; i < indicadores.Length; i++)
            {
                int index = i;
                if (indicadores[i] != null)
                {
                    indicadores[i].MouseLeftButtonUp -= (s, e) => { };
                    indicadores[i].MouseLeftButtonUp += (s, e) =>
                    {
                        _esNavegacionManual = true;
                        _indiceActual = index;
                        MostrarTarjetaActual();
                        ReiniciarTimer();
                    };
                }
            }
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            _viewModel.ActualizarCards();
            MostrarTarjetaActual();
        }
    }
}