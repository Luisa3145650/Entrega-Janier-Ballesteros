using loginavicola.Database;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using LiveCharts;
using LiveCharts.Wpf;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Runtime.CompilerServices;
using System.Windows.Media;

namespace loginavicola.ViewModel
{
    public class homeViewModel : INotifyPropertyChanged
    {
        private readonly LoteDatabase database;
        private readonly InventarioDatabase inventarioDb;
        private readonly DiagnosticoDatabase diagDb;
        // 1. Propiedad Total Lotes
        private int _totalLotes;
        public int TotalLotes
        {
            get => _totalLotes;
            set { _totalLotes = value; OnPropertyChanged(); }
        }

        // 2. Propiedad Lotes Activos
        private int _lotesActivos;
        public int LotesActivos
        {
            get => _lotesActivos;
            set { _lotesActivos = value; OnPropertyChanged(); }
        }

        // 3. Propiedad Total Aves
        private int _totalAves;
        public int TotalAves
        {
            get => _totalAves;
            set { _totalAves = value; OnPropertyChanged(); }
        }

        private decimal _totalAlimento;
        public decimal TotalAlimento
        {
            get => _totalAlimento;
            set { _totalAlimento = value; OnPropertyChanged(); }
        }

        private SeriesCollection _estadoAvesSeries;
        public SeriesCollection EstadoAvesSeries
        {
            get => _estadoAvesSeries;
            set { _estadoAvesSeries = value; OnPropertyChanged(); }
        }

        public homeViewModel()
        {
            database = new LoteDatabase();
            inventarioDb = new InventarioDatabase();
            diagDb = new DiagnosticoDatabase();
            ActualizarCards();
        }

        public void ActualizarCards()
        {
            // 1. Obtener datos base de lotes
            TotalLotes = database.ObtenerTotalLotes();
            LotesActivos = database.ObtenerLotesActivos();
            TotalAves = database.ObtenerTotalAves();

            // 2. Lógica Dinámica de Salud de Aves (Agrupada por Tipo de ComboBox)
            var listaDiagnosticos = diagDb.ObtenerTodosDiagnosticos();
            var series = new SeriesCollection();
            int totalAvesConDiagnostico = 0;

            if (listaDiagnosticos != null)
            {
                // Filtramos solo los casos que siguen "Activos"
                var casosActivos = listaDiagnosticos.Where(d => d.Estado == "Activo").ToList();

                // Agrupamos por el texto del ComboBox (Tipo)
                var gruposPorTipo = casosActivos
                    .GroupBy(d => d.Tipo)
                    .Select(g => new {
                        NombreTipo = g.Key,
                        SumaAves = g.Sum(d => d.GallinasAfectadas)
                    });

                foreach (var grupo in gruposPorTipo)
                {
                    var color = grupo.NombreTipo == "Enfermedad" ? Brushes.Red :
                                grupo.NombreTipo == "Prevención" ? Brushes.Orange :
                                Brushes.DodgerBlue;

                    series.Add(new PieSeries
                    {
                        Title = grupo.NombreTipo,
                        Values = new ChartValues<int> { grupo.SumaAves },
                        DataLabels = true,
                        Fill = color, // Asigna el color aquí
                        LabelPoint = p => $"{p.Y} aves"
                    });
                    totalAvesConDiagnostico += grupo.SumaAves;
                }
            }

            // 3. Calcular las aves Sanas (Total - todas las afectadas por cualquier diagnóstico)
            int avesSanas = TotalAves - totalAvesConDiagnostico;
            if (avesSanas < 0) avesSanas = 0;

            // Añadimos siempre la rebanada de "Sanas" al final
            series.Add(new PieSeries
            {
                Title = "Sanas",
                Values = new ChartValues<int> { avesSanas },
                Fill = System.Windows.Media.Brushes.MediumSeaGreen,
                DataLabels = true,
                LabelPoint = p => $"{p.Y} aves"
            });

            // Asignamos la colección completa a la propiedad que escucha el XAML
            EstadoAvesSeries = series;

            // 4. Lógica de Inventario de Alimento (Igual que antes)
            var listaItems = inventarioDb.ObtenerTodosItems();
            if (listaItems != null)
            {
                TotalAlimento = listaItems
                    .Where(i => i.Categoria != null && i.Categoria.ToLower().Contains("alimento"))
                    .Sum(i => (decimal)i.CantidadStock);
            }
            else
            {
                TotalAlimento = 0;
            }
        }

        // --- ESTE ES EL MÉTODO QUE TE FALTABA ---
        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}