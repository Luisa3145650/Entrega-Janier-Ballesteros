using System;
using System.Windows;
using loginavicola.Database;
using loginavicola.Model;
namespace loginavicola.View
{
    public partial class ManualView : Window
    {
        private ClasificacionProduccionDatabase database;
        private string _recolectorActual;
        private int _idLote;
        private string _nombreLote;

        // Constructor: recibe el lote ya seleccionado en produccionView, no se puede
        // clasificar manualmente sin haber elegido un lote primero.
        public ManualView(string nombreUsuario, int idLote, string nombreLote)
        {
            InitializeComponent();
            database = new ClasificacionProduccionDatabase();
            _recolectorActual = nombreUsuario;
            _idLote = idLote;
            _nombreLote = nombreLote;

            // Configuración inicial automática
            dpFecha.SelectedDate = DateTime.Now;
            txtRecolector.Text = _recolectorActual;
            txtRecolector.IsReadOnly = true; // Bloquea el campo para que sea automático

            txtLoteInfo.Text = string.IsNullOrEmpty(_nombreLote) ? "Lote: -" : _nombreLote;
        }

        private void btnRegistrar_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Validar que haya un lote seleccionado (debería venir siempre desde produccionView,
                // pero se valida aquí también por seguridad)
                if (_idLote <= 0)
                {
                    MessageBox.Show("No hay un lote válido seleccionado. Cierra esta ventana y selecciona un lote primero.", "Validación", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Validar fecha
                if (!dpFecha.SelectedDate.HasValue)
                {
                    MessageBox.Show("Por favor seleccione una fecha", "Validación", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                // Parsear cantidades (si el campo está vacío pone 0)
                int jumbo = int.TryParse(txtJumbo.Text, out int j) ? j : 0;
                int aaa = int.TryParse(txtAAA.Text, out int a3) ? a3 : 0;
                int aa = int.TryParse(txtAA.Text, out int a2) ? a2 : 0;
                int a = int.TryParse(txtA.Text, out int a1) ? a1 : 0;
                int b = int.TryParse(txtB.Text, out int b1) ? b1 : 0;
                int c = int.TryParse(txtC.Text, out int c1) ? c1 : 0;
                int total = jumbo + aaa + aa + a + b + c;
                if (total <= 0)
                {
                    MessageBox.Show("Debe ingresar al menos un huevo para registrar.", "Validación", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                // Crear objeto de clasificación, vinculado al lote seleccionado
                var clasificacion = new ClasificacionProduccion
                {
                    IdLote = _idLote,
                    Fecha = dpFecha.SelectedDate.Value.Date,
                    HoraInicio = DateTime.Now.ToString("HH:mm:ss"),
                    Recolector = _recolectorActual,
                    TipoClasificacion = "Manual",
                    Jumbo = jumbo,
                    AAA = aaa,
                    AA = aa,
                    A = a,
                    B = b,
                    C = c,
                    Total = total,
                    Observaciones = "Registro Manual"
                };
                // Guardar en Base de Datos
                if (database.InsertarClasificacion(clasificacion))
                {
                    MessageBox.Show($"✅ Registro Exitoso\nLote: {_nombreLote}\nRecolector: {_recolectorActual}\nTotal: {total} huevos",
                        "Información", MessageBoxButton.OK, MessageBoxImage.Information);
                    this.DialogResult = true; // Avisa a la ventana principal que hubo un cambio
                    this.Close();
                }
                else
                {
                    MessageBox.Show("No se pudo guardar el registro. Intenta de nuevo.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void btnCerrar_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }
    }
}
