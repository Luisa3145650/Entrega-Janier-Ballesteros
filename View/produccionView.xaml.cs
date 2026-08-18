using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using loginavicola.ViewModel;

namespace loginavicola.View
{
    public partial class produccionView : UserControl
    {
        public produccionViewModel ViewModel { get; }

        public produccionView()
        {
            InitializeComponent();

            ViewModel = new produccionViewModel();
            this.DataContext = ViewModel;

            btnCapturarFoto.Click += BtnCapturarFoto_Click;
            btnClasificacionManual.Click += BtnClasificacionManual_Click;
            cmbLote.SelectionChanged += CmbLote_SelectionChanged;

            this.Loaded += ProduccionView_Loaded;
            this.Unloaded += ProduccionView_Unloaded;
        }

        private void ProduccionView_Loaded(object sender, RoutedEventArgs e)
        {
            Window window = Window.GetWindow(this);
            if (window != null)
            {
                window.PreviewKeyDown -= Window_PreviewKeyDown;
                window.PreviewKeyDown += Window_PreviewKeyDown;
            }

            ViewModel.RefrescarUsuarioActual();
            ViewModel.IniciarBuclesPolling();
        }

        private void ProduccionView_Unloaded(object sender, RoutedEventArgs e)
        {
            ViewModel.DetenerBuclesPolling();

            Window window = Window.GetWindow(this);
            if (window != null)
            {
                window.PreviewKeyDown -= Window_PreviewKeyDown;
            }
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Space)
            {
                ViewModel.RegistrarHuevoManual();
                e.Handled = true;
            }
        }

        private void CmbLote_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cmbLote.SelectedItem is LoteComboItem item)
            {
                ViewModel.SeleccionarLoteItem(item);
            }
            else
            {
                ViewModel.SeleccionarLoteItem(null);
            }
        }

        private void BtnClasificacionManual_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel.IdLoteSeleccionado <= 0)
            {
                MessageBox.Show("Selecciona un lote antes de abrir la clasificación manual.", "Lote requerido", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string usuarioActual = ViewModel.UsuarioActualNombre ?? "Invitado";
            ManualView ventana = new ManualView(usuarioActual, ViewModel.IdLoteSeleccionado, ViewModel.NombreLoteSeleccionado);
            ventana.Owner = Window.GetWindow(this);
            if (ventana.ShowDialog() == true)
            {
                _ = ViewModel.CargarHistorialAsync();
                _ = ViewModel.ActualizarEstadisticasAsync();
            }
        }

        private void BtnCapturarFoto_Click(object sender, RoutedEventArgs e)
        {
            if (imgCamara.Source == null) return;

            var saveDialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "Imagen JPG|*.jpg",
                FileName = $"huevo_{DateTime.Now:yyyyMMdd_HHmmss}"
            };

            if (saveDialog.ShowDialog() == true)
            {
                try
                {
                    using (FileStream fs = new FileStream(saveDialog.FileName, FileMode.Create))
                    {
                        var encoder = new JpegBitmapEncoder();
                        encoder.Frames.Add(BitmapFrame.Create((BitmapSource)imgCamara.Source));
                        encoder.Save(fs);
                    }
                    MessageBox.Show("📸 Foto guardada correctamente.", "Captura de Foto", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error guardando imagen: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }
}