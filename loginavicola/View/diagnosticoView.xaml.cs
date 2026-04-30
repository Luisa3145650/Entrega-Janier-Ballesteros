using System.Windows;
using System.Windows.Controls;
using loginavicola.ViewModel;
using loginavicola.Model; // Asegúrate de tener este using para UserSession

namespace loginavicola.View
{
    public partial class diagnosticoView : UserControl
    {
        private DiagnosticoViewModel viewModel;

        public diagnosticoView()
        {
            InitializeComponent();
            // Asignamos el ViewModel
            viewModel = new DiagnosticoViewModel();
            this.DataContext = viewModel;
        }

        private void BtnRegistrarDiagnostico_Click(object sender, RoutedEventArgs e)
        {
            // Limpia el formulario antes de mostrarlo
            viewModel.LimpiarFormulario();
            ModalOverlay.Visibility = Visibility.Visible;
        }

        private void BtnCerrarModal_Click(object sender, RoutedEventArgs e)
        {
            ModalOverlay.Visibility = Visibility.Collapsed;
        }

        private void BtnGuardar_Click(object sender, RoutedEventArgs e)
        {
            // Ejecuta la lógica de guardado del ViewModel
            if (viewModel.GuardarDiagnostico())
            {
                // Si se guardó con éxito, cerramos el modal
                ModalOverlay.Visibility = Visibility.Collapsed;

                // Forzamos el refresco de la tabla para que aparezca el nombre del medicamento
                viewModel.CargarDatos();
            }
        }

        private void BtnExportarExcel_Click(object sender, RoutedEventArgs e)
        {
            // Usamos el Helper que creamos para exportar
            if (viewModel.Diagnosticos.Count > 0)
            {
                var listaExportar = new System.Collections.Generic.List<Model.Diagnostico>(viewModel.Diagnosticos);
                Helpers.ExcelHelper.ExportarAExcel(listaExportar, "Historial_Diagnosticos");
            }
            else
            {
                MessageBox.Show("No hay datos para exportar.");
            }
        }
    }
}