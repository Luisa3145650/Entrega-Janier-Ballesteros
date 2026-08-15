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
using loginavicola.Model;
using loginavicola.ViewModel;

namespace loginavicola.View
{
    public partial class gestionView : UserControl
    {
        private GestionViewModel viewModel;

        public gestionView()
        {
            InitializeComponent();
            viewModel = new GestionViewModel();
            this.DataContext = viewModel;
        }

        private void BtnAgregarUsuario_Click(object sender, RoutedEventArgs e)
        {
            viewModel.UsuarioActual = new Usuario();
            PasswordBox.Clear();
            ConfirmPasswordBox.Clear();
            ModalRegistroOverlay.Visibility = Visibility.Visible;
        }

        private void BtnCerrarModalRegistro_Click(object sender, RoutedEventArgs e)
        {
            ModalRegistroOverlay.Visibility = Visibility.Collapsed;
        }

        private void BtnGuardarUsuario_Click(object sender, RoutedEventArgs e)
        {
            string password = PasswordBox.Password;
            string confirmPassword = ConfirmPasswordBox.Password;

            if (viewModel.GuardarUsuario(PasswordBox.Password, ConfirmPasswordBox.Password))
            {
                ModalRegistroOverlay.Visibility = Visibility.Collapsed;
                PasswordBox.Clear();
                ConfirmPasswordBox.Clear();
            }
        }

        private void BtnGestionarPermisos_Click(object sender, RoutedEventArgs e)
        {
            // Al hacer clic en una tarjeta, asignamos el usuario seleccionado
            var button = sender as Button;
            var usuario = button.Tag as Usuario;
            viewModel.UsuarioSeleccionado = usuario;

            ModalPermisosOverlay.Visibility = Visibility.Visible;
        }

        private void BtnCerrarModalPermisos_Click(object sender, RoutedEventArgs e)
        {
            ModalPermisosOverlay.Visibility = Visibility.Collapsed;
        }

        private void BtnGuardarPermisos_Click(object sender, RoutedEventArgs e)
        {
            if (viewModel.ActualizarPermisos())
            {
                MessageBox.Show("Permisos actualizados exitosamente", "Éxito",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                ModalPermisosOverlay.Visibility = Visibility.Collapsed;
            }
        }
    }
}
