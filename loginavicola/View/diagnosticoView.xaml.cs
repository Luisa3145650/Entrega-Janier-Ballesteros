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
    public partial class diagnosticoView : UserControl
    {
        private DiagnosticoViewModel viewModel;

        public diagnosticoView()
        {
            InitializeComponent();
            viewModel = new DiagnosticoViewModel();
            this.DataContext = viewModel;
        }

        private void BtnRegistrarDiagnostico_Click(object sender, RoutedEventArgs e)
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
            if (viewModel.GuardarDiagnostico())
            {
                ModalOverlay.Visibility = Visibility.Collapsed;
            }
        }

        private void DataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

        }
    }
}
