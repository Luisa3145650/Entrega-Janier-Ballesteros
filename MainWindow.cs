#nullable disable

using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using loginavicola.View;
using loginavicola.Model;
using loginavicola.ViewModel;
using FontAwesome.Sharp;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace loginavicola
{
    public partial class MainWindow : Window
    {
        private static readonly HttpClient client = new HttpClient();

        public MainWindow()
        {
            InitializeComponent();

            var viewModel = new MainViewModel();
            viewModel.LoadCurrentUserData();
            this.DataContext = viewModel;

            MainContentArea.Content = new homeView();
            ActualizarInfoUsuario();
            AplicarPermisos();

            this.KeyDown += MainWindow_KeyDown;
        }

        private async void MainWindow_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Space)
            {
                if (MainContentArea.Content is produccionView vistaProduccion)
                {
                    await ConsultarHardwarePython(vistaProduccion);
                }
            }
        }

        private async Task ConsultarHardwarePython(produccionView vistaActiva)
        {
            string url = "http://localhost:5001/datos-huevo";

            try
            {
                HttpResponseMessage response = await client.GetAsync(url);

                if (response.IsSuccessStatusCode)
                {
                    string jsonResponse = await response.Content.ReadAsStringAsync();
                    var opciones = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    DatosHuevo datos = JsonSerializer.Deserialize<DatosHuevo>(jsonResponse, opciones);

                    vistaActiva.lblPesoReal.Text = $"{datos.Peso} g";
                    vistaActiva.lblCategoria.Text = datos.Categoria;
                    vistaActiva.lblVolumen.Text = $"{datos.Volumen_Real:F2} cm³";
                }
                else
                {
                    MessageBox.Show("El servidor de visión artificial devolvió un error de procesamiento.", "Error API", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"No se pudo conectar con el hardware (Python local caído): {ex.Message}", "Error de Comunicación", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ActualizarInfoUsuario()
        {
            if (UserSession.EsVisitante)
            {
                txtUserName.Text = "Visitante";
                txtAvatarInicial.Text = "V";
                popupUserName.Text = "Visitante";
                popupUserRol.Text = "Visitante";
            }
            else if (UserSession.UsuarioActual != null)
            {
                var user = UserSession.UsuarioActual;
                txtUserName.Text = user.Nombres;

                if (!string.IsNullOrEmpty(user.Nombres))
                {
                    txtAvatarInicial.Text = user.Nombres[0].ToString().ToUpper();
                }

                popupUserName.Text = $"{user.Nombres} {user.Apellidos}";
                popupUserRol.Text = user.Rol;
            }
        }

        private void AplicarPermisos()
        {
            if (UserSession.EsVisitante)
            {
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
                Btngestion.Visibility = (user.Rol == "Administrador") ? Visibility.Visible : Visibility.Collapsed;

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

        private void btnVolverLogin_Click(object sender, RoutedEventArgs e)
        {
            var confirmDialog = new ConfirmExitView
            {
                Owner = this
            };

            bool? resultado = confirmDialog.ShowDialog();

            if (resultado == true)
            {
                UserSession.UsuarioActual = null;
                UserSession.EsVisitante = false;

                loginView login = new loginView();
                login.Show();
                this.Close();
            }
        }

        // ==================== //
        // MENÚ HAMBURGUESA      //
        // ==================== //
        private bool _sidebarCollapsed = false;

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
            AnimarColumna(250, 60);

            foreach (var txt in GetMenuTexts())
                txt.Visibility = Visibility.Collapsed;

            PanelLogo.Visibility = Visibility.Collapsed;
        }

        private void ExpandirMenu()
        {
            AnimarColumna(60, 250);

            foreach (var txt in GetMenuTexts())
                txt.Visibility = Visibility.Visible;

            PanelLogo.Visibility = Visibility.Visible;
        }

        private void AnimarColumna(double desde, double hasta)
        {
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

            SidebarColumn.Width = new GridLength(hasta);
        }

        // ==================== //
        // MENÚ DESPLEGABLE USUARIO //
        // ==================== //
        private void btnUserMenu_Click(object sender, RoutedEventArgs e)
        {
            popupUserMenu.IsOpen = !popupUserMenu.IsOpen;
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (popupUserMenu.IsOpen)
            {
                var element = e.OriginalSource as DependencyObject;
                if (element != null && !IsChildOf(element, btnUserMenu))
                {
                    popupUserMenu.IsOpen = false;
                }
            }
        }

        private bool IsChildOf(DependencyObject child, DependencyObject parent)
        {
            while (child != null)
            {
                if (child == parent) return true;
                child = VisualTreeHelper.GetParent(child);
            }
            return false;
        }
    }
}