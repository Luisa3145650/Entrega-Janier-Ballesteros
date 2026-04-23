using System;
using System.Windows;
using System.Windows.Input;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using loginavicola.View;
using loginavicola.Model;
using loginavicola.ViewModel;
using FontAwesome.Sharp;

namespace loginavicola
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            // Creamos e inicializamos el ViewModel
            var viewModel = new loginavicola.ViewModel.MainViewModel();

            // Cargamos los datos del usuario
            viewModel.LoadCurrentUserData();

            // Conectamos el DataContext para que el XAML vea el Nombre y Rol
            this.DataContext = viewModel;

            MainContentArea.Content = new homeView();

            // Llamamos a la lógica de permisos al abrir la ventana
            AplicarPermisos();
        }

        private void AplicarPermisos()
        {
            // SOLUCIÓN AL ERROR: Especificamos que use el del Model para evitar la ambigüedad
            var user = loginavicola.Model.UserSession.UsuarioActual;

            if (user != null)
            {
                Btndashboar.Visibility = Visibility.Visible;

                // Regla especial: Solo el Administrador ve la Gestión de Usuarios
                Btngestion.Visibility = (user.Rol == "Administrador") ? Visibility.Visible : Visibility.Collapsed;

                // Permisos por módulos
                Btnlotes.Visibility = user.PermisoLotes ? Visibility.Visible : Visibility.Collapsed;
                BtnProduccion.Visibility = user.PermisoProduccion ? Visibility.Visible : Visibility.Collapsed;
                Btnalimentacion.Visibility = user.PermisoAlimentacion ? Visibility.Visible : Visibility.Collapsed;
                Btndiagnostico.Visibility = user.PermisoDiagnostico ? Visibility.Visible : Visibility.Collapsed;
                Btninventario.Visibility = user.PermisoInventario ? Visibility.Visible : Visibility.Collapsed;
                Btnreportes.Visibility = user.PermisoInventario ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private void pnlControlBar_MouseEnter(object sender, MouseEventArgs e)
        {
            this.MaxHeight = SystemParameters.MaximizedPrimaryScreenHeight;
        }

        private void pnlControlBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
                DragMove();
        }

        // Método genérico para navegar y cambiar el título/icono
        private void Navegar(object view, string titulo, IconChar icono)
        {
            MainContentArea.Content = view;
            txtWindowTitle.Text = titulo;
            imgTitleIcon.Icon = icono;
        }

        // Eventos Click de los botones
        private void Btndashboar_Click(object sender, RoutedEventArgs e) => Navegar(new homeView(), "Dashboard", IconChar.Home);

        private void Btnlotes_Click(object sender, RoutedEventArgs e) => Navegar(new lotesView(), "Lotes", IconChar.CheckToSlot);

        private void BtnProduccion_Click(object sender, RoutedEventArgs e) => Navegar(new produccionView(), "Producción", IconChar.Egg);

        private void Btnalimentacion_Click(object sender, RoutedEventArgs e) => Navegar(new alimentacionView(), "Alimentación", IconChar.Jar);

        private void Btndiagnostico_Click(object sender, RoutedEventArgs e) => Navegar(new diagnosticoView(), "Diagnóstico", IconChar.Stethoscope);

        private void Btninventario_Click(object sender, RoutedEventArgs e) => Navegar(new inventarioView(), "Inventario", IconChar.Warehouse);

        private void Btngestion_Click(object sender, RoutedEventArgs e) => Navegar(new gestionView(), "Gestión", IconChar.User);

        private void Btnreportes_Click(object sender, RoutedEventArgs e) => Navegar(new reportesView(), "Reportes", IconChar.Download);

        // Controles de ventana
        private void btnClose_Click(object sender, RoutedEventArgs e) => Application.Current.Shutdown();

        private void btnMinimize_Click(object sender, RoutedEventArgs e) => this.WindowState = WindowState.Minimized;

        private void btnMaximize_Click(object sender, RoutedEventArgs e) => this.WindowState = (this.WindowState == WindowState.Maximized) ? WindowState.Normal : WindowState.Maximized;

        private void btnVolverLogin_Click(object sender, RoutedEventArgs e)
        {
            // Limpiamos la sesión usando la ruta completa para evitar el error de ambigüedad
            loginavicola.Model.UserSession.UsuarioActual = null;

            new loginView().Show();
            this.Close();
        }
    }
}