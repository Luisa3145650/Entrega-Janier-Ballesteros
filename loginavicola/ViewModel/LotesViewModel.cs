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
        private System.Collections.Generic.List<Lote> _todosLosFiltrados = new();

        public string RolUsuarioActual => UserSession.UsuarioActual?.Rol ?? string.Empty;

        public LotesViewModel()
        {
            database = new LoteDatabase();

            System.Diagnostics.Debug.WriteLine($"=== ROL CARGADO: '{UserSession.UsuarioActual?.Rol}' ===");
            System.Diagnostics.Debug.WriteLine($"=== RolUsuarioActual: '{RolUsuarioActual}' ===");

            LotesRegistrados = new ObservableCollection<Lote>();

            EstadosDisponibles = new ObservableCollection<string>
            {
                "Activo",
                "Pensionado"
            };

            // CORREGIDO: Cambiado a 'OpcionesTamanoPagina' para que coincida con el XAML
            OpcionesTamanoPagina = new ObservableCollection<int> { 5, 10, 20, 50 };
            _tamanoPagina = 10;
            _paginaActual = 1;

            AbrirModalCommand = new RelayCommand(AbrirModal);
            CerrarModalCommand = new RelayCommand(CerrarModal);
            RegistrarCommand = new RelayCommand(RegistrarLote);
            EditarCommand = new RelayCommand(EditarLote);
            EliminarCommand = new RelayCommand(EliminarLote);
            PaginaAnteriorCommand = new RelayCommand(_ => CambiarPagina(-1), _ => PaginaActual > 1);
            PaginaSiguienteCommand = new RelayCommand(_ => CambiarPagina(1), _ => PaginaActual < TotalPaginas);

            CargarDatos();
        }

        // ── Estadísticas ──────────────────────────────────────────────
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

        // ── Paginación ────────────────────────────────────────────────
        private int _paginaActual;
        public int PaginaActual
        {
            get => _paginaActual;
            set
            {
                _paginaActual = value;
                OnPropertyChanged(nameof(PaginaActual));
                OnPropertyChanged(nameof(InfoPagina));
                AplicarPagina();
            }
        }

        private int _tamanoPagina;
        // CORREGIDO: Cambiado a 'TamanoPagina' (sin 'ñ') para emparejar con el XAML
        public int TamanoPagina
        {
            get => _tamanoPagina;
            set
            {
                _tamanoPagina = value;
                OnPropertyChanged(nameof(TamanoPagina));
                _paginaActual = 1;
                OnPropertyChanged(nameof(PaginaActual));
                RecalcularPaginas();
                AplicarPagina();
            }
        }

        private int _totalPaginas = 1;
        public int TotalPaginas
        {
            get => _totalPaginas;
            set
            {
                _totalPaginas = value;
                OnPropertyChanged(nameof(TotalPaginas));
                OnPropertyChanged(nameof(InfoPagina));
            }
        }

        public string InfoPagina => $"Página {PaginaActual} de {TotalPaginas}  ({_todosLosFiltrados.Count} registros)";

        // CORREGIDO: Cambiado a 'OpcionesTamanoPagina' (sin 'ñ')
        public ObservableCollection<int> OpcionesTamanoPagina { get; }

        // ── Colecciones y estado ──────────────────────────────────────
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

        // ── Comandos ──────────────────────────────────────────────────
        public ICommand AbrirModalCommand { get; }
        public ICommand CerrarModalCommand { get; }
        public ICommand RegistrarCommand { get; }
        public ICommand EditarCommand { get; }
        public ICommand EliminarCommand { get; }
        public ICommand PaginaAnteriorCommand { get; }
        public ICommand PaginaSiguienteCommand { get; }

        // ── Lógica interna ────────────────────────────────────────────
        private void CargarDatos()
        {
            var lotes = database.ObtenerTodosLosLotes();

            _todosLosFiltrados = string.IsNullOrWhiteSpace(TextoBusqueda)
                ? lotes
                : FiltrarLista(lotes, TextoBusqueda.ToLower());

            _paginaActual = 1;
            RecalcularPaginas();
            AplicarPagina();
            ActualizarEstadisticas();

            OnPropertyChanged(nameof(RolUsuarioActual));
            System.Diagnostics.Debug.WriteLine($"=== ROL EN CargarDatos: '{RolUsuarioActual}' ===");
        }

        private void ActualizarEstadisticas()
        {
            TotalLotes = database.ObtenerTotalLotes();
            LotesActivos = database.ObtenerLotesActivos();
            TotalAves = database.ObtenerTotalAves();
        }

        private void RecalcularPaginas()
        {
            // CORREGIDO: Reemplazado por TamanoPagina
            TotalPaginas = Math.Max(1, (int)Math.Ceiling(_todosLosFiltrados.Count / (double)TamanoPagina));
            if (_paginaActual > TotalPaginas) _paginaActual = TotalPaginas;
        }

        private void AplicarPagina()
        {
            // CORREGIDO: Reemplazado por TamanoPagina
            var pagina = _todosLosFiltrados
                .Skip((_paginaActual - 1) * TamanoPagina)
                .Take(TamanoPagina)
                .ToList();

            LotesRegistrados.Clear();
            foreach (var l in pagina) LotesRegistrados.Add(l);

            OnPropertyChanged(nameof(InfoPagina));
        }

        private void CambiarPagina(int delta)
        {
            var nueva = _paginaActual + delta;
            if (nueva >= 1 && nueva <= TotalPaginas)
                PaginaActual = nueva;
        }

        private void FiltrarLotes()
        {
            var todos = database.ObtenerTodosLosLotes();

            _todosLosFiltrados = string.IsNullOrWhiteSpace(TextoBusqueda)
                ? todos
                : FiltrarLista(todos, TextoBusqueda.ToLower());

            _paginaActual = 1;
            RecalcularPaginas();
            AplicarPagina();
        }

        private static System.Collections.Generic.List<Lote> FiltrarLista(
            System.Collections.Generic.List<Lote> lotes, string busqueda)
        {
            return lotes.Where(l =>
                (l.Raza?.ToLower().Contains(busqueda) ?? false) ||
                (l.GranjaOrigen?.ToLower().Contains(busqueda) ?? false) ||
                (l.Estado?.ToLower().Contains(busqueda) ?? false) ||
                l.IdLote.ToString().Contains(busqueda)
            ).ToList();
        }

        // ── Modal ─────────────────────────────────────────────────────
        private void AbrirModal(object parameter)
        {
            _esEdicion = false;
            LoteActual = new Lote { FechaIncorporacion = DateTime.Now, Estado = "Activo" };
            MostrarModal = true;
        }

        private void CerrarModal(object parameter) => MostrarModal = false;

        private void RegistrarLote(object parameter)
        {
            if (!ValidarLote()) return;

            bool resultado = _esEdicion
                ? database.ActualizarLote(LoteActual)
                : database.InsertarLote(LoteActual);

            if (resultado)
            {
                string msg = _esEdicion ? "Lote actualizado exitosamente" : "Lote registrado exitosamente";
                MessageBox.Show(msg, "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
                CargarDatos();
                MostrarModal = false;
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

                if (resultado == MessageBoxResult.Yes && database.EliminarLote(lote.IdLote))
                {
                    MessageBox.Show("Lote eliminado exitosamente", "Éxito",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    CargarDatos();
                }
            }
        }

        private bool ValidarLote()
        {
            if (string.IsNullOrWhiteSpace(LoteActual.Raza))
            {
                MessageBox.Show("Debe ingresar una raza", "Validación", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }
            if (LoteActual.CantidadGallinas <= 0)
            {
                MessageBox.Show("La cantidad debe ser mayor a 0", "Validación", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }
            if (string.IsNullOrWhiteSpace(LoteActual.Estado))
            {
                MessageBox.Show("Debe seleccionar un estado", "Validación", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }
            return true;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
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

        public bool CanExecute(object parameter) => _canExecute == null || _canExecute(parameter);
        public void Execute(object parameter) => _execute(parameter);
    }
}