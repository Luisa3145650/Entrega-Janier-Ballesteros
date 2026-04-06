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

            // Inicialización de Comandos
            MarcarComoResueltoCommand = new RelayCommand(MarcarComoResuelto);
            ReabrirCasoCommand = new RelayCommand(ReabrirCaso);
            EliminarDiagnosticoCommand = new RelayCommand(EliminarDiagnostico);

            // Carga inicial de datos
            CargarDatos();
            CargarLotesActivos();
            CargarMedicamentos();
        }

        // ✅ COMMANDS
        public ICommand MarcarComoResueltoCommand { get; }
        public ICommand ReabrirCasoCommand { get; }
        public ICommand EliminarDiagnosticoCommand { get; }

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
                FiltrarDiagnosticos();
            }
        }

        private void FiltrarDiagnosticos()
        {
            var listaCompleta = database.ObtenerTodosDiagnosticos();
            var inventario = inventarioDatabase.ObtenerTodosItems();
            Diagnosticos.Clear();

            var filtrados = listaCompleta.Where(d =>
                string.IsNullOrEmpty(TextoBusqueda) ||
                d.DiagnosticoMedico.ToLower().Contains(TextoBusqueda.ToLower()) ||
                d.Tipo.ToLower().Contains(TextoBusqueda.ToLower())
            );

            foreach (var d in filtrados)
            {
                // Cruce de nombre de medicamento en el filtrado
                if (d.IdMedicamento.HasValue)
                {
                    var med = inventario.FirstOrDefault(i => i.IdItem == d.IdMedicamento.Value);
                    d.NombreMedicamento = med != null ? med.Nombre : "ID no encontrado";
                }
                else { d.NombreMedicamento = "N/A"; }

                Diagnosticos.Add(d);
            }

            ActualizarEstadisticas();
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
        private void ActualizarEstadisticas()
        {
            TotalDiagnosticos = Diagnosticos.Count;
            CasosActivos = Diagnosticos.Count(d => d.Estado == "Activo");
            CasosResueltos = Diagnosticos.Count(d => d.Estado == "Resuelto");
            AvesAfectadas = Diagnosticos.Where(d => d.Estado == "Activo").Sum(d => d.GallinasAfectadas);
        }

        public void CargarDatos()
        {
            Diagnosticos.Clear();
            var lista = database.ObtenerTodosDiagnosticos();
            var inventario = inventarioDatabase.ObtenerTodosItems();

            foreach (var d in lista)
            {
                // Cruce de datos para obtener el nombre real del medicamento
                if (d.IdMedicamento.HasValue)
                {
                    var item = inventario.FirstOrDefault(i => i.IdItem == d.IdMedicamento.Value);
                    d.NombreMedicamento = item != null ? item.Nombre : "ID Inexistente";
                }
                else
                {
                    d.NombreMedicamento = "Sin Medicamento";
                }
                Diagnosticos.Add(d);
            }

            ActualizarEstadisticas();
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

            // Lógica de descuento de inventario
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
                CargarDatos(); // Recargar la tabla con nombres cruzados
                CargarMedicamentos(); // Actualizar stock visual
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