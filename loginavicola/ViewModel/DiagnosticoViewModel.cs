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

            MarcarComoResueltoCommand = new RelayCommand(MarcarComoResuelto);
            ReabrirCasoCommand = new RelayCommand(ReabrirCaso);
            EliminarDiagnosticoCommand = new RelayCommand(EliminarDiagnostico);

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

        // ✅ PROPIEDADES NUEVAS (Para solucionar los errores de Binding)
        private string _textoBusqueda;
        public string TextoBusqueda
        {
            get => _textoBusqueda;
            set
            {
                _textoBusqueda = value;
                OnPropertyChanged(nameof(TextoBusqueda));
                // Aquí podrías filtrar la lista si lo deseas
            }
        }

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

        // ✅ PROPIEDADES EXISTENTES
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

        // ============================================================
        // ACTUALIZAR ESTADÍSTICAS
        // ============================================================
        private void ActualizarEstadisticas()
        {
            TotalDiagnosticos = Diagnosticos.Count;
            CasosActivos = Diagnosticos.Count(d => d.Estado == "Activo");
            CasosResueltos = Diagnosticos.Count(d => d.Estado == "Resuelto");
AvesAfectadas = Diagnosticos.Where(d => d.Estado == "Activo").Sum(d => d.GallinasAfectadas);        }

        // ============================================================
        // MÉTODOS DE CARGA
        // ============================================================
        public void CargarDatos()
        {
            Diagnosticos.Clear();
            var lista = database.ObtenerTodosDiagnosticos();
            foreach (var d in lista)
                Diagnosticos.Add(d);

            ActualizarEstadisticas(); // Actualizamos los números de la interfaz
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
            foreach (var l in loteDatabase.ObtenerTodosLosLotes().Where(l => l.CantidadGallinas > 0))
                LotesActivos.Add(l);
        }

        // ============================================================
        // GUARDAR DIAGNOSTICO
        // ============================================================
        public bool GuardarDiagnostico()
        {
            if (!ValidarDiagnostico()) return false;

            if (DiagnosticoActual.IdMedicamento.HasValue && DiagnosticoActual.CantidadMedicamentoUsado > 0)
            {
                var medicamento = inventarioDatabase.ObtenerTodosItems()
                    .FirstOrDefault(i => i.IdItem == DiagnosticoActual.IdMedicamento.Value);

                if (medicamento == null) { MessageBox.Show("Medicamento no encontrado"); return false; }
                if (medicamento.CantidadStock < DiagnosticoActual.CantidadMedicamentoUsado) { MessageBox.Show("Stock insuficiente"); return false; }

                medicamento.CantidadStock -= DiagnosticoActual.CantidadMedicamentoUsado;
                inventarioDatabase.ActualizarItem(medicamento);
            }

            bool resultado = database.InsertarDiagnostico(DiagnosticoActual);
            if (resultado)
            {
                MessageBox.Show("Diagnóstico guardado correctamente");
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
                database.EliminarDiagnostico(diagnostico.IdDiagnostico);
                CargarDatos();
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

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}