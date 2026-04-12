using System;
using System.Windows;
using System.Windows.Input;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using loginavicola.View;
using loginavicola.Model; // Asegúrate de que aquí esté tu clase Usuario
using FontAwesome.Sharp;

namespace loginavicola
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            MainContentArea.Content = new homeView(); // Vista inicial

            // Llamamos a la lógica de permisos al abrir la ventana
            AplicarPermisos();
        }

        private void AplicarPermisos()
        {
            // Asumimos que guardaste el usuario en una clase estática llamada UserSession al hacer Login
            var user = UserSession.UsuarioActual;

            if (user != null)
            {
                Btndashboar.Visibility = Visibility.Visible;
                // Regla especial: Solo el Administrador ve la Gestión de Usuarios
                Btngestion.Visibility = (user.Rol == "Administrador") ? Visibility.Visible : Visibility.Collapsed;

                // Si las propiedades son bool, se usan directamente así:
                Btnlotes.Visibility = user.PermisoLotes ? Visibility.Visible : Visibility.Collapsed;
                BtnProduccion.Visibility = user.PermisoProduccion ? Visibility.Visible : Visibility.Collapsed;
                Btnalimentacion.Visibility = user.PermisoAlimentacion ? Visibility.Visible : Visibility.Collapsed;
                Btndiagnostico.Visibility = user.PermisoDiagnostico ? Visibility.Visible : Visibility.Collapsed;

                // Nota: Aquí usé el permiso que corresponde a reportes según tu lógica
                Btninventario.Visibility = user.PermisoInventario ? Visibility.Visible : Visibility.Collapsed;
                Btnreportes.Visibility = user.PermisoInventario ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private void pnlControlBar_MouseEnter(object sender, MouseEventArgs e)
        {
            // Esto ayuda a que la ventana no tape la barra de tareas al maximizar
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

        // Eventos Click de los botones (Llaman al método Navegar)
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
            new loginView().Show();
            this.Close();
        }
    }
}