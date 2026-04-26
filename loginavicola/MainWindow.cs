using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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
            var viewModel = new MainViewModel();

            // Cargamos los datos del usuario
            viewModel.LoadCurrentUserData();

            // Conectamos el DataContext
            this.DataContext = viewModel;

            MainContentArea.Content = new homeView();

            // Actualizar textos de usuario
            ActualizarInfoUsuario();

            // Aplicar permisos
            AplicarPermisos();
        }

        private void ActualizarInfoUsuario()
        {
            if (UserSession.EsVisitante)
            {
                txtUserName.Text = "Visitante";
                txtUserRol.Text = "Visitante";
            }
            else if (UserSession.UsuarioActual != null)
            {
                txtUserName.Text = UserSession.UsuarioActual.Nombres;
                txtUserRol.Text = UserSession.UsuarioActual.Rol;
            }
        }

        private void AplicarPermisos()
        {
            if (UserSession.EsVisitante)
            {
                // Visitante: solo ve Dashboard y Producción
                Btndashboar.Visibility = Visibility.Visible;
                BtnProduccion.Visibility = Visibility.Visible;

                Btnlotes.Visibility = Visibility.Collapsed;
                Btnalimentacion.Visibility = Visibility.Collapsed;
                Btndiagnostico.Visibility = Visibility.Collapsed;
                Btninventario.Visibility = Visibility.Collapsed;
                Btngestion.Visibility = Visibility.Collapsed;
                Btnreportes.Visibility = Visibility.Collapsed;
            }
            else if (UserSession.UsuarioActual != null)
            {
                var user = UserSession.UsuarioActual;

                Btndashboar.Visibility = Visibility.Visible;

                // Solo el Administrador ve la Gestión de Usuarios
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

        private void Navegar(object view, string titulo, IconChar icono)
        {
            MainContentArea.Content = view;
            txtWindowTitle.Text = titulo;
            imgTitleIcon.Icon = icono;
        }

        private void Btndashboar_Click(object sender, RoutedEventArgs e)
        {
            Navegar(new homeView(), "Dashboard", IconChar.Home);
        }

        private void Btnlotes_Click(object sender, RoutedEventArgs e)
        {
            if (UserSession.EsVisitante)
            {
                MessageBox.Show("Acceso denegado. Los visitantes solo pueden ver Dashboard y Producción.", "Permiso denegado", MessageBoxButton.OK, MessageBoxImage.Warning);
                Btndashboar.IsChecked = true;
                return;
            }
            Navegar(new lotesView(), "Lotes", IconChar.CheckToSlot);
        }

        private void BtnProduccion_Click(object sender, RoutedEventArgs e)
        {
            Navegar(new produccionView(), "Producción", IconChar.Egg);
        }

        private void Btnalimentacion_Click(object sender, RoutedEventArgs e)
        {
            if (UserSession.EsVisitante)
            {
                MessageBox.Show("Acceso denegado. Los visitantes solo pueden ver Dashboard y Producción.", "Permiso denegado", MessageBoxButton.OK, MessageBoxImage.Warning);
                Btndashboar.IsChecked = true;
                return;
            }
            Navegar(new alimentacionView(), "Alimentación", IconChar.Jar);
        }

        private void Btndiagnostico_Click(object sender, RoutedEventArgs e)
        {
            if (UserSession.EsVisitante)
            {
                MessageBox.Show("Acceso denegado. Los visitantes solo pueden ver Dashboard y Producción.", "Permiso denegado", MessageBoxButton.OK, MessageBoxImage.Warning);
                Btndashboar.IsChecked = true;
                return;
            }
            Navegar(new diagnosticoView(), "Diagnóstico", IconChar.Stethoscope);
        }

        private void Btninventario_Click(object sender, RoutedEventArgs e)
        {
            if (UserSession.EsVisitante)
            {
                MessageBox.Show("Acceso denegado. Los visitantes solo pueden ver Dashboard y Producción.", "Permiso denegado", MessageBoxButton.OK, MessageBoxImage.Warning);
                Btndashboar.IsChecked = true;
                return;
            }
            Navegar(new inventarioView(), "Inventario", IconChar.Warehouse);
        }

        private void Btngestion_Click(object sender, RoutedEventArgs e)
        {
            if (UserSession.EsVisitante || UserSession.UsuarioActual?.Rol != "Administrador")
            {
                MessageBox.Show("Acceso denegado. Solo administradores pueden acceder a esta sección.", "Permiso denegado", MessageBoxButton.OK, MessageBoxImage.Warning);
                Btndashboar.IsChecked = true;
                return;
            }
            Navegar(new gestionView(), "Gestión de usuarios", IconChar.User);
        }

        private void Btnreportes_Click(object sender, RoutedEventArgs e)
        {
            if (UserSession.EsVisitante)
            {
                MessageBox.Show("Acceso denegado. Los visitantes no pueden exportar datos.", "Permiso denegado", MessageBoxButton.OK, MessageBoxImage.Warning);
                Btndashboar.IsChecked = true;
                return;
            }
            Navegar(new reportesView(), "Exportar Datos", IconChar.Download);
        }

        private void btnClose_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void btnMinimize_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }

        private void btnMaximize_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = (this.WindowState == WindowState.Maximized) ? WindowState.Normal : WindowState.Maximized;
        }

        // ══════════════════════════════════════════════════
        //  MENÚ HAMBURGUESA
        // ══════════════════════════════════════════════════

        private bool _sidebarCollapsed = false;

        // Todos los TextBlock del menú agrupados para fácil control
        private IEnumerable<TextBlock> GetMenuTexts() => new[]
        {
    TxtInicio, TxtLotes, TxtProduccion, TxtAlimentacion,
    TxtDiagnostico, TxtInventario, TxtGestion, TxtReportes,
    TxtCerrarSesion
};

        private void btnHamburger_Click(object sender, RoutedEventArgs e)
        {
            _sidebarCollapsed = !_sidebarCollapsed;

            if (_sidebarCollapsed)
                ColapsarMenu();
            else
                ExpandirMenu();
        }

        private void ColapsarMenu()
        {
            // Anima el ancho de la columna de 250 → 60
            AnimarColumna(250, 60);

            // Oculta textos y logo
            foreach (var txt in GetMenuTexts())
                txt.Visibility = Visibility.Collapsed;

            PanelLogo.Visibility = Visibility.Collapsed;

            // Los tooltips se muestran al hacer hover (ya configurado en XAML)
        }

        private void ExpandirMenu()
        {
            // Anima el ancho de la columna de 60 → 250
            AnimarColumna(60, 250);

            // Muestra textos y logo
            foreach (var txt in GetMenuTexts())
                txt.Visibility = Visibility.Visible;

            PanelLogo.Visibility = Visibility.Visible;
        }

        private void AnimarColumna(double desde, double hasta)
        {
            // Usamos MaxWidth del Border del sidebar para animar suavemente
            SidebarBorder.BeginAnimation(
                FrameworkElement.MaxWidthProperty,
                new System.Windows.Media.Animation.DoubleAnimation
                {
                    From = desde,
                    To = hasta,
                    Duration = new Duration(TimeSpan.FromMilliseconds(220)),
                    EasingFunction = new System.Windows.Media.Animation.CubicEase
                    {
                        EasingMode = System.Windows.Media.Animation.EasingMode.EaseInOut
                    }
                }
            );

            // Actualiza también el ancho de la columna para que el área principal se reajuste
            SidebarColumn.Width = new GridLength(hasta);
        }

        private void btnVolverLogin_Click(object sender, RoutedEventArgs e)
        {
            UserSession.UsuarioActual = null;
            UserSession.EsVisitante = false;

            loginView login = new loginView();
            login.Show();
            this.Close();
        }
    }
}