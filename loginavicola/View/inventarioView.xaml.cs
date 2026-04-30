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
using System.Windows.Shapes;
using loginavicola.ViewModel;

namespace loginavicola.View
{
    public partial class inventarioView : UserControl
    {
        private InventarioViewModel viewModel;

        public inventarioView()
        {
            InitializeComponent();
            viewModel = new InventarioViewModel();
            this.DataContext = viewModel;
        }

        private void BtnRegistrarProducto_Click(object sender, RoutedEventArgs e)
        {
            viewModel.LimpiarFormulario();
            ModalOverlay.Visibility = Visibility.Visible;
        }

        private void BtnCerrarModal_Click(object sender, RoutedEventArgs e)
        {
            ModalOverlay.Visibility = Visibility.Collapsed;
        }

        private void BtnGuardar_Click(object sender, RoutedEventArgs e)
        {
            if (viewModel.GuardarItem())
            {
                ModalOverlay.Visibility = Visibility.Collapsed;
            }
        }

        private void BtnEditar_Click(object sender, RoutedEventArgs e)
        {
            // Validación para abrir el editor
            if (!UserSession.EsAdministrador)
            {
                MessageBox.Show("Solo el administrador puede editar el inventario.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            ModalOverlay.Visibility = Visibility.Visible;
        }
    }
}
