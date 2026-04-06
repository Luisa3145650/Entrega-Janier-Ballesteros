using LiveCharts;
using LiveCharts.Defaults;
using LiveCharts.Wpf;
using loginavicola.ViewModel;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Separator = LiveCharts.Wpf.Separator;

namespace loginavicola.View
{
    public partial class homeView : UserControl
    {
        public homeView()
        {
            InitializeComponent();
            //CargarGraficaTorta();
            //CargarGraficaEstadoAves();
            this.DataContext = new homeViewModel();
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is homeViewModel vm)
            {
                vm.ActualizarCards();
            }
        }

        private void CargarGraficaTorta()
        {
            CategoriaChart.Series = new SeriesCollection
            {
                new PieSeries
                {
                    Title = "Huevo A",
                    Values = new ChartValues<double> { 450 },
                    Fill = new SolidColorBrush(Color.FromRgb(16, 185, 129)),   // verde
                    DataLabels = true,
                    LabelPoint = p => $"{p.Y} ({p.Participation:P0})"
                },
                new PieSeries
                {
                    Title = "Huevo B",
                    Values = new ChartValues<double> { 280 },
                    Fill = new SolidColorBrush(Color.FromRgb(78, 205, 196)),   // turquesa
                    DataLabels = true,
                    LabelPoint = p => $"{p.Y} ({p.Participation:P0})"
                },
                new PieSeries
                {
                    Title = "Huevo C",
                    Values = new ChartValues<double> { 160 },
                    Fill = new SolidColorBrush(Color.FromRgb(168, 85, 247)),   // morado
                    DataLabels = true,
                    LabelPoint = p => $"{p.Y} ({p.Participation:P0})"
                }
            };
        }

        private void CargarGraficaEstadoAves()
        {
            EstadoAvesChart.Series = new SeriesCollection
            {
                new PieSeries
                {
                    Title = "Activas",
                    Values = new ChartValues<double> { 1100 },
                    Fill = new SolidColorBrush(Color.FromRgb(34, 197, 94)),    // verde
                    DataLabels = true,
                    LabelPoint = p => $"{p.Y} ({p.Participation:P0})"
                },
                new PieSeries
                {
                    Title = "En cuarentena",
                    Values = new ChartValues<double> { 85 },
                    Fill = new SolidColorBrush(Color.FromRgb(251, 191, 36)),   // amarillo
                    DataLabels = true,
                    LabelPoint = p => $"{p.Y} ({p.Participation:P0})"
                },
                new PieSeries
                {
                    Title = "Bajas",
                    Values = new ChartValues<double> { 65 },
                    Fill = new SolidColorBrush(Color.FromRgb(239, 68, 68)),    // rojo
                    DataLabels = true,
                    LabelPoint = p => $"{p.Y} ({p.Participation:P0})"
                }
            };
        }
    }
}