using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows.Input;
using loginavícola.Model;
using loginavicola.Helpers;
using System.Collections.Generic;
using loginavicola.Database;
using System.Windows;

using ModelAlimento = loginavicola.Model.Alimento;
using ModelConsumo = loginavicola.Model.Consumo;
using ModelLoteGallina = loginavicola.Model.LoteGallina;

namespace loginavicola.ViewModel
{
    public class AlimentacionViewModel : INotifyPropertyChanged
    {
        private readonly ConsumoDatabase database;
        private readonly InventarioDatabase inventarioDatabase;

        private ModelConsumo _consumoActual = new ModelConsumo
        {
            FechaConsumo = DateTime.Now,
            UnidadMedida = "kg",
            Turno = "Semanal"
        };

        private string _textoBusqueda = string.Empty;
        private decimal _consumoDia;
        private decimal _consumoSemanal;
        private decimal _alimentoDisponible;

        // ✅ FIX: Propiedad para el alimento seleccionado directamente
        private ModelAlimento? _alimentoSeleccionado;
        public ModelAlimento? AlimentoSeleccionado
        {
            get => _alimentoSeleccionado;
            set
            {
                _alimentoSeleccionado = value;
                OnPropertyChanged(nameof(AlimentoSeleccionado));

                // ✅ Actualiza IdAlimento en ConsumoActual automáticamente
                if (value != null && ConsumoActual != null)
                {
                    ConsumoActual.IdAlimento = value.IdAlimento;
                    OnPropertyChanged(nameof(ConsumoActual));
                }
            }
        }

        public AlimentacionViewModel()
        {
            database = new ConsumoDatabase();
            inventarioDatabase = new InventarioDatabase();

            Consumos = new ObservableCollection<ModelConsumo>();
            Alimentos = new ObservableCollection<ModelAlimento>();
            LotesActivos = new ObservableCollection<ModelLoteGallina>();
            UnidadesMedida = new ObservableCollection<string> { "kg" };
            Turnos = new ObservableCollection<string> { "Semanal" };

            CargarDatos();
        }

        public decimal ConsumoDia
        {
            get => _consumoDia;
            set { _consumoDia = value; OnPropertyChanged(nameof(ConsumoDia)); }
        }

        public decimal ConsumoSemanal
        {
            get => _consumoSemanal;
            set { _consumoSemanal = value; OnPropertyChanged(nameof(ConsumoSemanal)); }
        }

        public decimal AlimentoDisponible
        {
            get => _alimentoDisponible;
            set { _alimentoDisponible = value; OnPropertyChanged(nameof(AlimentoDisponible)); }
        }

        public ObservableCollection<ModelConsumo> Consumos { get; set; }
        public ObservableCollection<ModelAlimento> Alimentos { get; set; }
        public ObservableCollection<ModelLoteGallina> LotesActivos { get; set; }
        public ObservableCollection<string> UnidadesMedida { get; set; }
        public ObservableCollection<string> Turnos { get; set; }

        public ModelConsumo ConsumoActual
        {
            get => _consumoActual;
            set { _consumoActual = value; OnPropertyChanged(nameof(ConsumoActual)); }
        }

        public string TextoBusqueda
        {
            get => _textoBusqueda;
            set
            {
                _textoBusqueda = value;
                OnPropertyChanged(nameof(TextoBusqueda));
                FiltrarConsumos();
            }
        }

        public void CargarDatos()
        {
            CargarConsumos();
            CargarAlimentos();
            CargarLotes();
            ActualizarEstadisticas();
        }

        private void CargarConsumos()
        {
            Consumos.Clear();
            var consumos = database.ObtenerConsumos();
            foreach (var consumo in consumos)
                Consumos.Add(consumo);
        }

        private void CargarAlimentos()
        {
            Alimentos.Clear();

            // ✅ FIX: Categoría insensible a mayúsculas
            var itemsInventario = inventarioDatabase.ObtenerTodosItems()
                .Where(i => i.Categoria.ToLower().Contains("alimento")
                         && i.CantidadStock > 0)
                .ToList();

            // ✅ DEBUG: Muestra qué encontró en inventario
            if (itemsInventario.Count == 0)
            {
                // Verificar si existen items pero con otra categoría o sin stock
                var todosItems = inventarioDatabase.ObtenerTodosItems();
                string detalleItems = todosItems.Any()
                    ? string.Join("\n", todosItems.Select(i =>
                        $"• {i.Nombre} | Categoría: '{i.Categoria}' | Stock: {i.CantidadStock}"))
                    : "No hay productos registrados en inventario.";

                MessageBox.Show(
                    $"No se encontraron alimentos disponibles.\n\n" +
                    $"Productos en inventario:\n{detalleItems}\n\n" +
                    $"Asegúrese de que:\n" +
                    $"1. La categoría sea 'Alimento'\n" +
                    $"2. El stock actual sea mayor a 0",
                    "Sin Alimentos Disponibles",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            foreach (var item in itemsInventario)
            {
                Alimentos.Add(new ModelAlimento
                {
                    IdAlimento = item.IdItem,
                    Nombre = item.Nombre,
                    StockDisponible = item.CantidadStock,
                    UnidadMedida = "kg"
                });
            }
        }

        private void CargarLotes()
        {
            LotesActivos.Clear();
            var lotes = database.ObtenerLotesActivos();
            foreach (var lote in lotes)
                LotesActivos.Add(lote);
        }

        private void ActualizarEstadisticas()
        {
            ConsumoDia = database.ObtenerConsumoDia();
            ConsumoSemanal = database.ObtenerConsumoSemanal();

            var totalAlimentoKg = inventarioDatabase.ObtenerTodosItems()
                .Where(i => i.Categoria.ToLower().Contains("alimento"))
                .Sum(i => (decimal)i.CantidadStock);

            AlimentoDisponible = totalAlimentoKg;
        }

        public bool GuardarConsumo()
        {
            if (ValidarConsumo())
            {
                var loteSeleccionado = LotesActivos
                    .FirstOrDefault(l => l.IdLote == ConsumoActual.IdLoteGallinas);

                if (loteSeleccionado == null)
                {
                    MessageBox.Show("No se pudo obtener la información del lote.", "Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return false;
                }

                ConsumoActual.CantidadGallinas = loteSeleccionado.CantidadActual;

                var (consumoEsperado, merma, alertaMerma) = database.CalcularConsumo(
                    ConsumoActual.CantidadGallinas,
                    ConsumoActual.CantidadConsumida);

                ConsumoActual.ConsumoEsperado = consumoEsperado;
                ConsumoActual.Merma = merma;
                ConsumoActual.AlertaMerma = alertaMerma;

                // ✅ FIX: Usa AlimentoSeleccionado directamente (más confiable)
                string nombreAlimento = AlimentoSeleccionado?.Nombre ?? "Concentrado";

                // Validar stock suficiente
                if (AlimentoSeleccionado != null &&
                    ConsumoActual.CantidadConsumida > AlimentoSeleccionado.StockDisponible)
                {
                    MessageBox.Show(
                        $"⚠️ Stock insuficiente para '{nombreAlimento}'.\n\n" +
                        $"Stock disponible: {AlimentoSeleccionado.StockDisponible} kg\n" +
                        $"Cantidad requerida: {ConsumoActual.CantidadConsumida} kg",
                        "Stock Insuficiente",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return false;
                }

                string iconoMerma = alertaMerma ? "⚠️" : "✅";
                string mensajeMerma = alertaMerma
                    ? $"\n\n{iconoMerma} ALERTA: Merma excesiva detectada"
                    : $"\n\n{iconoMerma} Merma dentro del rango permitido";

                var resultado = MessageBox.Show(
                    $"📊 RESUMEN DE CONSUMO SEMANAL\n" +
                    $"━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n\n" +
                    $"🐔 Lote: {loteSeleccionado.IdLote} - {loteSeleccionado.Raza}\n" +
                    $"📌 Gallinas: {ConsumoActual.CantidadGallinas}\n" +
                    $"🌾 Alimento: {nombreAlimento}\n" +
                    $"📦 Stock actual: {AlimentoSeleccionado?.StockDisponible} kg\n" +
                    $"📦 Stock tras registro: {AlimentoSeleccionado?.StockDisponible - ConsumoActual.CantidadConsumida} kg\n\n" +
                    $"📋 RACIÓN DIARIA POR GALLINA:\n" +
                    $"   • Mañana: 60g | Tarde: 60g | Total: 120g\n\n" +
                    $"📊 CONSUMO:\n" +
                    $"   • Esperado: {consumoEsperado:F2} kg\n" +
                    $"   • Registrado: {ConsumoActual.CantidadConsumida:F2} kg\n" +
                    $"   • Diferencia: {merma:F2} kg" +
                    mensajeMerma + "\n\n¿Confirmar registro?",
                    "Confirmar Registro Semanal",
                    MessageBoxButton.YesNo,
                    alertaMerma ? MessageBoxImage.Warning : MessageBoxImage.Question);

                if (resultado == MessageBoxResult.Yes)
                {
                    bool guardado = database.InsertarConsumoSemanal(ConsumoActual);

                    if (guardado)
                    {
                        DescontarStockInventario(
                            ConsumoActual.IdAlimento,
                            (int)ConsumoActual.CantidadConsumida);

                        CargarDatos();
                        LimpiarFormulario();
                    }
                    return guardado;
                }
            }
            return false;
        }

        private void DescontarStockInventario(int idAlimento, int cantidad)
        {
            bool descontado = inventarioDatabase.ActualizarStock(idAlimento, cantidad, "resta");

            if (!descontado)
            {
                MessageBox.Show(
                    "El consumo fue registrado pero hubo un error al actualizar el stock.\n" +
                    "Por favor verifique el inventario manualmente.",
                    "Advertencia de Stock",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }

        private bool ValidarConsumo()
        {
            if (ConsumoActual.IdLoteGallinas == 0)
            {
                MessageBox.Show("Debe seleccionar un lote.", "Validación",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            // ✅ FIX: Valida usando AlimentoSeleccionado en lugar de solo IdAlimento
            if (AlimentoSeleccionado == null || ConsumoActual.IdAlimento == 0)
            {
                MessageBox.Show("Debe seleccionar un alimento.", "Validación",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (ConsumoActual.CantidadConsumida <= 0)
            {
                MessageBox.Show("La cantidad debe ser mayor a 0.", "Validación",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            var loteSeleccionado = LotesActivos
                .FirstOrDefault(l => l.IdLote == ConsumoActual.IdLoteGallinas);

            if (loteSeleccionado != null)
            {
                decimal consumoEsperado = loteSeleccionado.CantidadActual * 0.6m;
                decimal consumoMaximo = consumoEsperado * 2;

                if (ConsumoActual.CantidadConsumida > consumoMaximo)
                {
                    var resultado = MessageBox.Show(
                        $"⚠️ Cantidad muy alta ({ConsumoActual.CantidadConsumida:F2} kg).\n\n" +
                        $"Consumo esperado: {consumoEsperado:F2} kg\n" +
                        $"¿Está seguro?",
                        "Validar Cantidad",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Warning);

                    return resultado == MessageBoxResult.Yes;
                }
            }

            return true;
        }

        public void LimpiarFormulario()
        {
            ConsumoActual = new ModelConsumo
            {
                FechaConsumo = DateTime.Now,
                UnidadMedida = "kg",
                Turno = "Semanal"
            };

            // ✅ FIX: Limpiar también el alimento seleccionado
            AlimentoSeleccionado = null;
        }

        private void FiltrarConsumos()
        {
            if (string.IsNullOrWhiteSpace(TextoBusqueda))
            {
                CargarConsumos();
                return;
            }

            var consumosFiltrados = database.ObtenerConsumos()
                .Where(c =>
                    c.NombreAlimento.ToLower().Contains(TextoBusqueda.ToLower()) ||
                    c.IdLoteGallinas.ToString().Contains(TextoBusqueda) ||
                    c.Turno.ToLower().Contains(TextoBusqueda.ToLower()))
                .ToList();

            Consumos.Clear();
            foreach (var consumo in consumosFiltrados)
                Consumos.Add(consumo);
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}