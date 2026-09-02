using System;
using System.Collections.Generic;
using System.ComponentModel;
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
    public partial class lotesView : UserControl
    {
        private LotesViewModel viewModel;

        public lotesView()
        {
            InitializeComponent();
            viewModel = new LotesViewModel();
            this.DataContext = viewModel;
        }

        private void BtnRegistrarLote_Click(object sender, RoutedEventArgs e)
        {
            viewModel.LoteActual = new Model.Lote 
            { 
                FechaIncorporacion = DateTime.Now, 
                Estado = "En Producción" 
            };
            ModalOverlay.Visibility = Visibility.Visible;
        }

        private void BtnCerrarModal_Click(object sender, RoutedEventArgs e)
        {
            ModalOverlay.Visibility = Visibility.Collapsed;
        }

        private void BtnGuardar_Click(object sender, RoutedEventArgs e)
        {
            if (viewModel.RegistrarCommand.CanExecute(null))
            {
                viewModel.RegistrarCommand.Execute(null);
                ModalOverlay.Visibility = Visibility.Collapsed;
            }
        }

        private void BtnEditarLote_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is Model.Lote lote)
            {
                viewModel.LoteActual = lote;
                ModalOverlay.Visibility = Visibility.Visible;
            }
        }
    }
}
