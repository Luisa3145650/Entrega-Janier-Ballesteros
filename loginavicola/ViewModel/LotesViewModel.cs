using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using loginavicola.Database;
using loginavicola.Model;

namespace loginavicola.ViewModel
{
    public class LotesViewModel : INotifyPropertyChanged
    {
        private readonly LoteDatabase database;

        public LotesViewModel()
        {
            database = new LoteDatabase();

            LotesRegistrados = new ObservableCollection<Lote>();

            // Estados disponibles para el ComboBox
            EstadosDisponibles = new ObservableCollection<string>
            {
                "Activo",
                "Pensionado"
            };

            AbrirModalCommand = new RelayCommand(AbrirModal);
            CerrarModalCommand = new RelayCommand(CerrarModal);
            RegistrarCommand = new RelayCommand(RegistrarLote);
            EditarCommand = new RelayCommand(EditarLote);
            EliminarCommand = new RelayCommand(EliminarLote);

            CargarDatos();
        }

        private int _totalLotes;
        public int TotalLotes
        {
            get => _totalLotes;
            set { _totalLotes = value; OnPropertyChanged(nameof(TotalLotes)); }
        }

        private int _lotesActivos;
        public int LotesActivos
        {
            get => _lotesActivos;
            set { _lotesActivos = value; OnPropertyChanged(nameof(LotesActivos)); }
        }

        private int _totalAves;
        public int TotalAves
        {
            get => _totalAves;
            set { _totalAves = value; OnPropertyChanged(nameof(TotalAves)); }
        }

        public ObservableCollection<Lote> LotesRegistrados { get; set; }
        public ObservableCollection<string> EstadosDisponibles { get; set; }

        private Lote _loteActual = new Lote { FechaIncorporacion = DateTime.Now };
        public Lote LoteActual
        {
            get => _loteActual;
            set { _loteActual = value; OnPropertyChanged(nameof(LoteActual)); }
        }

        private bool _mostrarModal;
        public bool MostrarModal
        {
            get => _mostrarModal;
            set { _mostrarModal = value; OnPropertyChanged(nameof(MostrarModal)); }
        }

        private string _textoBusqueda = string.Empty;
        public string TextoBusqueda
        {
            get => _textoBusqueda;
            set
            {
                _textoBusqueda = value;
                OnPropertyChanged(nameof(TextoBusqueda));
                FiltrarLotes();
            }
        }

        private bool _esEdicion;

        public ICommand AbrirModalCommand { get; }
        public ICommand CerrarModalCommand { get; }
        public ICommand RegistrarCommand { get; }
        public ICommand EditarCommand { get; }
        public ICommand EliminarCommand { get; }

        private void CargarDatos()
        {
            LotesRegistrados.Clear();
            var lotes = database.ObtenerTodosLosLotes();

            foreach (var lote in lotes)
            {
                LotesRegistrados.Add(lote);
            }

            ActualizarEstadisticas();
        }

        private void ActualizarEstadisticas()
        {
            TotalLotes = database.ObtenerTotalLotes();
            LotesActivos = database.ObtenerLotesActivos();
            TotalAves = database.ObtenerTotalAves();
        }

        private void AbrirModal(object parameter)
        {
            _esEdicion = false;
            LoteActual = new Lote { FechaIncorporacion = DateTime.Now, Estado = "Activo" };
            MostrarModal = true;
        }

        private void CerrarModal(object parameter)
        {
            MostrarModal = false;
        }

        private void RegistrarLote(object parameter)
        {
            if (ValidarLote())
            {
                bool resultado;

                if (_esEdicion)
                {
                    resultado = database.ActualizarLote(LoteActual);
                    if (resultado)
                    {
                        MessageBox.Show("Lote actualizado exitosamente", "Éxito",
                            MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
                else
                {
                    resultado = database.InsertarLote(LoteActual);
                    if (resultado)
                    {
                        MessageBox.Show("Lote registrado exitosamente", "Éxito",
                            MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }

                if (resultado)
                {
                    CargarDatos();
                    MostrarModal = false;
                }
            }
        }

        private void EditarLote(object parameter)
        {
            if (parameter is Lote lote)
            {
                _esEdicion = true;
                LoteActual = new Lote
                {
                    IdLote = lote.IdLote,
                    Raza = lote.Raza,
                    CantidadGallinas = lote.CantidadGallinas,
                    FechaIncorporacion = lote.FechaIncorporacion,
                    GranjaOrigen = lote.GranjaOrigen,
                    Estado = lote.Estado,
                    Observaciones = lote.Observaciones
                };
                MostrarModal = true;
            }
        }

        private void EliminarLote(object parameter)
        {
            if (parameter is Lote lote)
            {
                var resultado = MessageBox.Show(
                    $"¿Está seguro de eliminar el lote {lote.IdLote}?",
                    "Confirmar eliminación",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (resultado == MessageBoxResult.Yes)
                {
                    if (database.EliminarLote(lote.IdLote))
                    {
                        MessageBox.Show("Lote eliminado exitosamente", "Éxito",
                            MessageBoxButton.OK, MessageBoxImage.Information);
                        CargarDatos();
                    }
                }
            }
        }

        private bool ValidarLote()
        {
            if (string.IsNullOrWhiteSpace(LoteActual.Raza))
            {
                MessageBox.Show("Debe ingresar una raza", "Validación",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (LoteActual.CantidadGallinas <= 0)
            {
                MessageBox.Show("La cantidad debe ser mayor a 0", "Validación",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (string.IsNullOrWhiteSpace(LoteActual.Estado))
            {
                MessageBox.Show("Debe seleccionar un estado", "Validación",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            return true;
        }

        private void FiltrarLotes()
        {
            var todosLosLotes = database.ObtenerTodosLosLotes();

            if (string.IsNullOrWhiteSpace(TextoBusqueda))
            {
                LotesRegistrados.Clear();
                foreach (var l in todosLosLotes) LotesRegistrados.Add(l);
                return;
            }

            var busqueda = TextoBusqueda.ToLower();
            var filtrados = todosLosLotes.Where(l =>
                (l.Raza?.ToLower().Contains(busqueda) ?? false) ||
                (l.GranjaOrigen?.ToLower().Contains(busqueda) ?? false) ||
                (l.Estado?.ToLower().Contains(busqueda) ?? false) ||
                l.IdLote.ToString().Contains(busqueda)
            ).ToList();

            LotesRegistrados.Clear();
            foreach (var lote in filtrados) LotesRegistrados.Add(lote);
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class RelayCommand : ICommand
    {
        private readonly Action<object> _execute;
        private readonly Func<object, bool> _canExecute;

        public RelayCommand(Action<object> execute, Func<object, bool> canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public event EventHandler CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        public bool CanExecute(object parameter)
        {
            return _canExecute == null || _canExecute(parameter);
        }

        public void Execute(object parameter)
        {
            _execute(parameter);
        }
    }
}