using System;
using System.Collections.Generic;
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
using System.Windows.Shapes;
using loginavicola.Database;
using loginavicola.Model;

namespace loginavicola.View
{
    public partial class ManualView : Window
    {
        private ClasificacionProduccionDatabase database;

        public ManualView()
        {
            InitializeComponent();
            database = new ClasificacionProduccionDatabase();
            dpFecha.SelectedDate = DateTime.Now;
        }

        private void btnRegistrar_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Validar recolector
                if (string.IsNullOrWhiteSpace(txtRecolector.Text))
                {
                    MessageBox.Show("Por favor ingrese el nombre del recolector",
                        "Validación", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Validar fecha
                if (!dpFecha.SelectedDate.HasValue)
                {
                    MessageBox.Show("Por favor seleccione una fecha",
                        "Validación", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Parsear cantidades
                int jumbo = int.TryParse(txtJumbo.Text, out int j) && j >= 0 ? j : 0;
                int aaa = int.TryParse(txtAAA.Text, out int a3) && a3 >= 0 ? a3 : 0;
                int aa = int.TryParse(txtAA.Text, out int a2) && a2 >= 0 ? a2 : 0;
                int a = int.TryParse(txtA.Text, out int a1) && a1 >= 0 ? a1 : 0;
                int b = int.TryParse(txtB.Text, out int b1) && b1 >= 0 ? b1 : 0;
                int c = int.TryParse(txtC.Text, out int c1) && c1 >= 0 ? c1 : 0;

                int total = jumbo + aaa + aa + a + b + c;

                // Validar que haya al menos un huevo
                if (total == 0)
                {
                    MessageBox.Show("Debe clasificar al menos un huevo",
                        "Validación", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Crear objeto de clasificación
                var clasificacion = new ClasificacionProduccion
                {
                    Fecha = dpFecha.SelectedDate.Value.Date,
                    Hora = DateTime.Now.TimeOfDay, // HORA ACTUAL
                    Recolector = txtRecolector.Text.Trim(),
                    TipoClasificacion = "Manual", // TIPO: MANUAL
                    Jumbo = jumbo,
                    AAA = aaa,
                    AA = aa,
                    A = a,
                    B = b,
                    C = c,
                    Total = total,
                    Observaciones = "Clasificación manual registrada"
                };

                // Guardar en base de datos
                if (database.InsertarClasificacion(clasificacion))
                {
                    MessageBox.Show(
                        $"✅ Clasificación Manual Registrada\n\n" +
                        $"📅 Fecha: {clasificacion.Fecha:dd/MM/yyyy}\n" +
                        $"🕐 Hora: {clasificacion.Hora:hh\\:mm\\:ss}\n" +
                        $"👷 Recolector: {clasificacion.Recolector}\n\n" +
                        $"🥚 Total: {total} huevos\n\n" +
                        $"Jumbo: {jumbo} | AAA: {aaa} | AA: {aa}\n" +
                        $"A: {a} | B: {b} | C: {c}",
                        "Registro Exitoso",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);

                    DialogResult = true;
                    Close();
                }
                else
                {
                    MessageBox.Show("Error al guardar la clasificación",
                        "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void btnCerrar_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}

