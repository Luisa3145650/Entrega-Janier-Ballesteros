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
            // 1. Obtener datos base
            TotalLotes = database.ObtenerTotalLotes();
            LotesActivos = database.ObtenerLotesActivos();
            TotalAves = database.ObtenerTotalAves();

            // 2. Lógica de Salud de Aves
            var listaDiagnosticos = diagDb.ObtenerTodosDiagnosticos();
            int avesEnfermas = 0;

            if (listaDiagnosticos != null)
            {
                avesEnfermas = listaDiagnosticos
                    .Where(d => d.Estado == "Activo" && d.Tipo == "Enfermedad")
                    .Sum(d => d.GallinasAfectadas);
            }

            int avesSanas = TotalAves - avesEnfermas;
            if (avesSanas < 0) avesSanas = 0;

            // Actualizamos la serie de la torta
            EstadoAvesSeries = new SeriesCollection
    {
        new PieSeries
        {
            Title = "Sanas",
            Values = new ChartValues<int> { avesSanas },
            Fill = System.Windows.Media.Brushes.MediumSeaGreen,
            DataLabels = true
        },
        new PieSeries
        {
            Title = "Enfermas",
            Values = new ChartValues<int> { avesEnfermas },
            Fill = System.Windows.Media.Brushes.IndianRed,
            DataLabels = true
        }
    };

            // 3. Lógica de Inventario de Alimento
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