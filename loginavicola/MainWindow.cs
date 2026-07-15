using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using loginavicola.View;
using loginavicola.Model;
using loginavicola.ViewModel;
using FontAwesome.Sharp;
// ══════════════════════════════════════════════════
// NUEVAS DIRECTIVAS PARA CONECTAR LA API DE PYTHON
// ══════════════════════════════════════════════════
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace loginavicola
{
    public partial class MainWindow : Window
    {
        // Instancia única de HttpClient para evitar agotar sockets de red
        private static readonly HttpClient client = new HttpClient();

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

            // ══════════════════════════════════════════════════
            // ESCUCHAR TECLADO PARA LA API
            // ══════════════════════════════════════════════════
            this.KeyDown += MainWindow_KeyDown;
        }

        // ══════════════════════════════════════════════════
        // EVENTO DEL TECLADO (BARRA ESPACIADORA)
        // ══════════════════════════════════════════════════
        private async void MainWindow_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Space)
            {
                // Solo ejecutamos la consulta si la vista actual es la de Producción
                if (MainContentArea.Content is produccionView vistaProduccion)
                {
                    await ConsultarHardwarePython(vistaProduccion);
                }
            }
        }

        // ══════════════════════════════════════════════════
        // CONSUMO DE LA API DE VISIÓN ARTIFICIAL Y BÁSCULA
        // ══════════════════════════════════════════════════
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

                    // Asignamos las lecturas reales a los TextBlocks de produccionView
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

        // ══════════════════════════════════════════════════
        //  CERRAR SESIÓN (con confirmación estilo moderno)
        // ══════════════════════════════════════════════════
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
    }
}