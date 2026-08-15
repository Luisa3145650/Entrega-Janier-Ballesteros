using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using loginavicola.Model;
using loginavicola.Helpers;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using System.Windows;
using loginavicola.Database;

namespace loginavicola.ViewModel
{
    public class DiagnosticoViewModel : INotifyPropertyChanged
    {
        // ✅ BASES DE DATOS
        private readonly DiagnosticoDatabase database;
        private readonly LoteDatabase loteDatabase;
        private readonly InventarioDatabase inventarioDatabase;

        // ✅ LISTA PRIVADA PARA ALMACENAR LOS REGISTROS ORIGINALES SIN PAGINAR
        private List<Diagnostico> _listaDiagnosticosCompleta = new List<Diagnostico>();

        // ✅ CONSTRUCTOR
        public DiagnosticoViewModel()
        {
            _textoBusqueda = string.Empty;
            database = new DiagnosticoDatabase();
            loteDatabase = new LoteDatabase();
            inventarioDatabase = new InventarioDatabase();

            Diagnosticos = new ObservableCollection<Diagnostico>();
            LotesActivos = new ObservableCollection<Lote>();
            MedicamentosDisponibles = new ObservableCollection<ItemInventario>();

            TiposDiagnostico = new ObservableCollection<string>
            {
                "Enfermedad",
                "Prevención",
                "Control",
                "Revisión"
            };

            // Inicialización de Comandos Existentes
            MarcarComoResueltoCommand = new RelayCommand(MarcarComoResuelto);
            ReabrirCasoCommand = new RelayCommand(ReabrirCaso);
            EliminarDiagnosticoCommand = new RelayCommand(EliminarDiagnostico);

            // 🛠️ FUNCIONALIDAD FALTANTE: Inicialización de Comandos de Paginación
            PaginaAnteriorCommand = new RelayCommand(_ => PaginaAnterior(), _ => PaginaActual > 1);
            PaginaSiguienteCommand = new RelayCommand(_ => PaginaSiguiente(), _ => PaginaActual < TotalPaginas);

            // Carga inicial de datos
            CargarDatos();
            CargarLotesActivos();
            CargarMedicamentos();
        }

        // ✅ COMMANDS
        public ICommand MarcarComoResueltoCommand { get; }
        public ICommand ReabrirCasoCommand { get; }
        public ICommand EliminarDiagnosticoCommand { get; }

        // 🛠️ FUNCIONALIDAD FALTANTE: Comandos de paginación
        public ICommand PaginaAnteriorCommand { get; }
        public ICommand PaginaSiguienteCommand { get; }

        // ✅ COLECCIONES
        public ObservableCollection<Diagnostico> Diagnosticos { get; set; }
        public ObservableCollection<Lote> LotesActivos { get; set; }
        public ObservableCollection<string> TiposDiagnostico { get; set; }
        public ObservableCollection<ItemInventario> MedicamentosDisponibles { get; set; }

        // ✅ PROPIEDADES DE BÚSQUEDA Y FILTRADO
        private string _textoBusqueda = string.Empty;
        public string TextoBusqueda
        {
            get => _textoBusqueda;
            set
            {
                _textoBusqueda = value;
                OnPropertyChanged(nameof(TextoBusqueda));
                PaginaActual = 1; // Reiniciar a la primera página al buscar
                FiltrarYPaginar();
            }
        }

        // 🛠️ FUNCIONALIDAD FALTANTE: Propiedades de Control de Paginación
        private int _elementosPorPagina = 10;
        public string ElementosPorPagina
        {
            get => _elementosPorPagina.ToString();
            set
            {
                if (int.TryParse(value, out int result))
                {
                    _elementosPorPagina = result;
                    OnPropertyChanged(nameof(ElementosPorPagina));
                    PaginaActual = 1; // Reiniciar a la página 1 cuando cambie la escala
                    FiltrarYPaginar();
                }
            }
        }

        private int _paginaActual = 1;
        public int PaginaActual
        {
            get => _paginaActual;
            set
            {
                _paginaActual = value;
                OnPropertyChanged(nameof(PaginaActual));
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
            }
        }


        // 🛠️ MÉTODO OPTIMIZADO: Filtra la lista completa y aplica Skip / Take para segmentar por páginas
        private void FiltrarYPaginar()
        {
            var inventario = inventarioDatabase.ObtenerTodosItems();

            // 1. Filtrar la lista en memoria según la búsqueda
            var filtrados = _listaDiagnosticosCompleta.Where(d =>
                string.IsNullOrEmpty(TextoBusqueda) ||
                (d.DiagnosticoMedico != null && d.DiagnosticoMedico.ToLower().Contains(TextoBusqueda.ToLower())) ||
                (d.Tipo != null && d.Tipo.ToLower().Contains(TextoBusqueda.ToLower()))
            ).ToList();

            // 2. Calcular estadísticas en base al universo filtrado total
            TotalDiagnosticos = filtrados.Count;
            CasosActivos = filtrados.Count(d => d.Estado == "Activo");
            CasosResueltos = filtrados.Count(d => d.Estado == "Resuelto");
            AvesAfectadas = filtrados.Where(d => d.Estado == "Activo").Sum(d => d.GallinasAfectadas);

            // 3. Calcular el total de páginas necesarias
            TotalPaginas = (int)Math.Ceiling((double)TotalDiagnosticos / _elementosPorPagina);
            if (TotalPaginas == 0) TotalPaginas = 1;

            // Asegurar que la página actual no quede desfasada si se reduce drásticamente el volumen de datos
            if (PaginaActual > TotalPaginas) PaginaActual = TotalPaginas;

            // 4. Segmentar datos con LINQ (Skip y Take hacen la magia de paginación)
            var datosPaginados = filtrados
                .Skip((PaginaActual - 1) * _elementosPorPagina)
                .Take(_elementosPorPagina);

            // 5. Limpiar y rellenar la colección observable para la UI
            Diagnosticos.Clear();
            foreach (var d in datosPaginados)
            {
                if (d.IdMedicamento.HasValue)
                {
                    var med = inventario.FirstOrDefault(i => i.IdItem == d.IdMedicamento.Value);
                    d.NombreMedicamento = med != null ? med.Nombre : "ID no encontrado";
                }
                else { d.NombreMedicamento = "N/A"; }

                Diagnosticos.Add(d);
            }
        }

        // 🛠️ ACCIONES DE PAGINACIÓN
        private void PaginaAnterior()
        {
            if (PaginaActual > 1)
            {
                PaginaActual--;
                FiltrarYPaginar();
            }
        }

        private void PaginaSiguiente()
        {
            if (PaginaActual < TotalPaginas)
            {
                PaginaActual++;
                FiltrarYPaginar();
            }
        }


        // ✅ ESTADÍSTICAS
        private int _totalDiagnosticos;
        public int TotalDiagnosticos
        {
            get => _totalDiagnosticos;
            set { _totalDiagnosticos = value; OnPropertyChanged(nameof(TotalDiagnosticos)); }
        }

        private int _casosActivos;
        public int CasosActivos
        {
            get => _casosActivos;
            set { _casosActivos = value; OnPropertyChanged(nameof(CasosActivos)); }
        }

        private int _casosResueltos;
        public int CasosResueltos
        {
            get => _casosResueltos;
            set { _casosResueltos = value; OnPropertyChanged(nameof(CasosResueltos)); }
        }

        private int _avesAfectadas;
        public int AvesAfectadas
        {
            get => _avesAfectadas;
            set { _avesAfectadas = value; OnPropertyChanged(nameof(AvesAfectadas)); }
        }

        // ✅ PROPIEDAD PARA EL FORMULARIO
        private Diagnostico _diagnosticoActual = new Diagnostico
        {
            FechaDiagnostico = DateTime.Now,
            Estado = "Activo"
        };

        public Diagnostico DiagnosticoActual
        {
            get => _diagnosticoActual;
            set { _diagnosticoActual = value; OnPropertyChanged(nameof(DiagnosticoActual)); }
        }

        // ✅ MÉTODOS DE ACTUALIZACIÓN Y CARGA
        public void CargarDatos()
        {
            // Descarga el bruto de la base de datos a una lista interna una sola vez por actualización
            _listaDiagnosticosCompleta = database.ObtenerTodosDiagnosticos();

            // Ejecuta el flujo combinado de filtrado, estadísticas y corte por páginas
            FiltrarYPaginar();
        }

        private void CargarMedicamentos()
        {
            MedicamentosDisponibles.Clear();
            var medicamentos = inventarioDatabase.ObtenerTodosItems()
                .Where(i => i.Categoria == "Medicamento" && i.CantidadStock > 0)
                .ToList();

            foreach (var med in medicamentos)
                MedicamentosDisponibles.Add(med);
        }

        private void CargarLotesActivos()
        {
            LotesActivos.Clear();
            var lotes = loteDatabase.ObtenerTodosLosLotes().Where(l => l.CantidadGallinas > 0);
            foreach (var l in lotes)
                LotesActivos.Add(l);
        }

        // ✅ LÓGICA DE GUARDADO
        public bool GuardarDiagnostico()
        {
            if (!ValidarDiagnostico()) return false;

            if (DiagnosticoActual.IdMedicamento.HasValue && DiagnosticoActual.CantidadMedicamentoUsado > 0)
            {
                var medicamento = inventarioDatabase.ObtenerTodosItems()
                    .FirstOrDefault(i => i.IdItem == DiagnosticoActual.IdMedicamento.Value);

                if (medicamento == null) { MessageBox.Show("Medicamento no encontrado"); return false; }
                if (medicamento.CantidadStock < DiagnosticoActual.CantidadMedicamentoUsado) { MessageBox.Show("Stock insuficiente en inventario"); return false; }

                medicamento.CantidadStock -= DiagnosticoActual.CantidadMedicamentoUsado;
                inventarioDatabase.ActualizarItem(medicamento);
            }

            bool resultado = database.InsertarDiagnostico(DiagnosticoActual);
            if (resultado)
            {
                CargarDatos();
                CargarMedicamentos();
                LimpiarFormulario();
                return true;
            }
            return false;
        }

        private bool ValidarDiagnostico()
        {
            if (string.IsNullOrWhiteSpace(DiagnosticoActual.Tipo)) { MessageBox.Show("Debe seleccionar un tipo"); return false; }
            if (DiagnosticoActual.IdLote <= 0) { MessageBox.Show("Debe seleccionar un lote"); return false; }
            if (string.IsNullOrWhiteSpace(DiagnosticoActual.DiagnosticoMedico)) { MessageBox.Show("Debe ingresar descripción"); return false; }
            return true;
        }

        // ✅ ACCIONES DE LA TABLA (COMANDOS)
        private void MarcarComoResuelto(object parameter)
        {
            if (parameter is Diagnostico diagnostico)
            {
                diagnostico.Estado = "Resuelto";
                database.ActualizarDiagnostico(diagnostico);
                CargarDatos();
            }
        }

        private void ReabrirCaso(object parameter)
        {
            if (parameter is Diagnostico diagnostico)
            {
                diagnostico.Estado = "Activo";
                database.ActualizarDiagnostico(diagnostico);
                CargarDatos();
            }
        }

        private void EliminarDiagnostico(object parameter)
        {
            if (parameter is Diagnostico diagnostico)
            {
                var result = MessageBox.Show("¿Está seguro de eliminar este diagnóstico?", "Confirmar", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (result == MessageBoxResult.Yes)
                {
                    database.EliminarDiagnostico(diagnostico.IdDiagnostico);
                    CargarDatos();
                }
            }
        }

        public void LimpiarFormulario()
        {
            DiagnosticoActual = new Diagnostico
            {
                FechaDiagnostico = DateTime.Now,
                Estado = "Activo"
            };
        }

        // ✅ NOTIFICACIÓN DE CAMBIOS
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}