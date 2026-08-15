using loginavicola.ViewModel;
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
using System.Windows.Navigation;
using loginavicola.Helpers;
using System.Windows.Shapes;

namespace loginavicola.View
{
    public partial class alimentacionView : UserControl
    {
        private AlimentacionViewModel viewModel;

        public alimentacionView()
        {
            InitializeComponent();
            viewModel = new AlimentacionViewModel();
            this.DataContext = viewModel;
        }

        private void BtnRegistrarConsumo_Click(object sender, RoutedEventArgs e)
        {
            // Mostrar el modal
            ModalOverlay.Visibility = Visibility.Visible;
        }

        private void BtnCerrarModal_Click(object sender, RoutedEventArgs e)
        {
            // Ocultar el modal
            ModalOverlay.Visibility = Visibility.Collapsed;
            viewModel.LimpiarFormulario();
        }

        private void BtnGuardar_Click(object sender, RoutedEventArgs e)
        {
            if (viewModel.GuardarConsumo())
            {
                MessageBox.Show("Consumo registrado exitosamente", "Éxito",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                ModalOverlay.Visibility = Visibility.Collapsed;
            }
        }
    }
}