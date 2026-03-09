using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

// LIBRERÍAS DE VIDEO
using AForge.Video;
using AForge.Video.DirectShow;

// LIBRERÍAS DE VISIÓN
using Emgu.CV;
using Emgu.CV.Structure;
using Emgu.CV.CvEnum;

using loginavicola.Database;
using loginavicola.Model;

namespace loginavicola.View
{
    public partial class produccionView : UserControl
    {
        private FilterInfoCollection dispositivosVideo;
        private VideoCaptureDevice fuenteVideo;
        private bool camaraConectada = false;
        private double factorConversion = 0.1;

        // BASE DE DATOS
        private ClasificacionProduccionDatabase database;

        // Contadores para clasificación automática
        private int contadorJumbo = 0;
        private int contadorAAA = 0;
        private int contadorAA = 0;
        private int contadorA = 0;
        private int contadorB = 0;
        private int contadorC = 0;

        public produccionView()
        {
            InitializeComponent();
            database = new ClasificacionProduccionDatabase();
            InitializeComponentEventHandlers();
            CargarCamarasUSB();
            ActualizarEstadisticas();
            CargarHistorial();
        }

        private void InitializeComponentEventHandlers()
        {
            btnConectarCamara.Click += (s, e) => ConectarCamaraUSB();
            btnDesconectarCamara.Click += (s, e) => DesconectarCamaraUSB();
            btnRefrescarCamaras.Click += (s, e) => { DesconectarCamaraUSB(); CargarCamarasUSB(); };
            btnCapturarFoto.Click += BtnCapturarFoto_Click;
            btnGuardar.Click += BtnGuardarClasificacionAutomatica_Click;
            this.Unloaded += (s, e) => DesconectarCamaraUSB();
        }

        private void btnClasificacionManual_Click(object sender, RoutedEventArgs e)
        {
            ManualView ventanaManual = new ManualView();
            if (ventanaManual.ShowDialog() == true)
            {
                ActualizarEstadisticas();
                CargarHistorial();
            }
        }

        // ─────────────────────────────────────────────────────────────
        // CORREGIDO: ahora muestra errores detallados al cargar cámaras
        // ─────────────────────────────────────────────────────────────
        private void CargarCamarasUSB()
        {
            try
            {
                dispositivosVideo = new FilterInfoCollection(FilterCategory.VideoInputDevice);
                cbCamaras.Items.Clear();

                // DIAGNÓSTICO: escribe en la consola de depuración de Visual Studio
                System.Diagnostics.Debug.WriteLine($"[Cámaras] Total detectadas: {dispositivosVideo.Count}");

                if (dispositivosVideo.Count > 0)
                {
                    foreach (FilterInfo d in dispositivosVideo)
                    {
                        System.Diagnostics.Debug.WriteLine($"  → {d.Name} | {d.MonikerString}");
                        cbCamaras.Items.Add(new CamaraUSB { Nombre = d.Name, MonikerString = d.MonikerString });
                    }

                    cbCamaras.SelectedIndex = 0;
                    ActualizarEstado($"✅ {dispositivosVideo.Count} cámara(s) detectada(s)");
                }
                else
                {
                    ActualizarEstado("⚠️ No se encontraron cámaras");
                    MessageBox.Show(
                        "AForge no detectó ninguna cámara.\n\n" +
                        "Posibles causas:\n" +
                        "• La cámara no está conectada\n" +
                        "• Falta instalar el driver del dispositivo\n" +
                        "• Windows está bloqueando el acceso (Configuración → Privacidad → Cámara)\n" +
                        "• Otra aplicación (Teams, Zoom, OBS) está usando la cámara",
                        "Sin cámaras detectadas",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                // CORREGIDO: antes el error se ignoraba silenciosamente
                string mensaje = $"Error al cargar cámaras:\n{ex.Message}";
                ActualizarEstado($"❌ Error al cargar cámaras");
                System.Diagnostics.Debug.WriteLine($"[ERROR CargarCamarasUSB] {ex}");
                MessageBox.Show(mensaje, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ─────────────────────────────────────────────────────────────
        // CORREGIDO: ahora tiene try-catch y valida que haya item seleccionado
        // ─────────────────────────────────────────────────────────────
        private void ConectarCamaraUSB()
        {
            try
            {
                // CORREGIDO: antes si SelectedItem no era CamaraUSB, fallaba silenciosamente
                if (cbCamaras.SelectedItem is not CamaraUSB cam)
                {
                    MessageBox.Show(
                        "Selecciona una cámara de la lista antes de conectar.",
                        "Aviso",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                // Si ya hay una cámara corriendo, la detenemos primero
                if (fuenteVideo != null && fuenteVideo.IsRunning)
                {
                    fuenteVideo.SignalToStop();
                    fuenteVideo.WaitForStop();
                    fuenteVideo = null;
                }

                ReiniciarContadores();

                fuenteVideo = new VideoCaptureDevice(cam.MonikerString);

                // CORREGIDO: verificar que el dispositivo tenga capacidades de video
                if (fuenteVideo.VideoCapabilities == null || fuenteVideo.VideoCapabilities.Length == 0)
                {
                    MessageBox.Show(
                        $"La cámara '{cam.Nombre}' no reportó capacidades de video.\n\n" +
                        "Prueba con otra cámara o reinstala el driver.",
                        "Sin capacidades de video",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                // Seleccionar la resolución más alta disponible
                fuenteVideo.VideoResolution = fuenteVideo.VideoCapabilities[0];
                System.Diagnostics.Debug.WriteLine(
                    $"[Cámara] Resolución seleccionada: " +
                    $"{fuenteVideo.VideoResolution.FrameSize.Width}x{fuenteVideo.VideoResolution.FrameSize.Height}");

                fuenteVideo.NewFrame += VideoSource_NewFrame;
                fuenteVideo.Start();

                camaraConectada = true;
                ActualizarEstado($"🔗 '{cam.Nombre}' conectada — Clasificación automática activa");
            }
            catch (Exception ex)
            {
                // CORREGIDO: antes no había try-catch aquí
                camaraConectada = false;
                System.Diagnostics.Debug.WriteLine($"[ERROR ConectarCamaraUSB] {ex}");
                MessageBox.Show(
                    $"No se pudo conectar la cámara:\n\n{ex.Message}\n\n" +
                    "Verifica que el dispositivo no esté siendo usado por otra aplicación.",
                    "Error al conectar",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                ActualizarEstado("❌ Error al conectar la cámara");
            }
        }

        private void VideoSource_NewFrame(object sender, NewFrameEventArgs eventArgs)
        {
            try
            {
                using (Bitmap bitmap = (Bitmap)eventArgs.Frame.Clone())
                {
                    using (Image<Bgr, byte> emguImage = BitmapToImage(bitmap))
                    {
                        ProcesarLogicaHuevo(emguImage);

                        using (Bitmap procesado = ImageToBitmap(emguImage))
                        {
                            var bsource = ConvertBitmapToBitmapSource(procesado);
                            if (bsource != null)
                            {
                                bsource.Freeze();
                                Dispatcher.BeginInvoke(new Action(() => imgCamara.Source = bsource));
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // CORREGIDO: antes el catch estaba vacío y ocultaba errores de procesamiento
                System.Diagnostics.Debug.WriteLine($"[ERROR VideoSource_NewFrame] {ex.Message}");
            }
        }

        private void ProcesarLogicaHuevo(Image<Bgr, byte> imagen)
        {
            try
            {
                using (var gris = imagen.Convert<Gray, byte>().SmoothGaussian(5))
                using (var binaria = gris.ThresholdBinaryInv(new Gray(150), new Gray(255)))
                using (var contornos = new Emgu.CV.Util.VectorOfVectorOfPoint())
                {
                    CvInvoke.FindContours(binaria, contornos, null, RetrType.External, ChainApproxMethod.ChainApproxSimple);

                    for (int i = 0; i < contornos.Size; i++)
                    {
                        if (CvInvoke.ContourArea(contornos[i]) > 3000)
                        {
                            RotatedRect elipse = CvInvoke.FitEllipse(contornos[i]);

                            double largo = Math.Max(elipse.Size.Width, elipse.Size.Height) * factorConversion;
                            double ancho = Math.Min(elipse.Size.Width, elipse.Size.Height) * factorConversion;
                            double vol = ((4.0 / 3.0) * Math.PI * Math.Pow(ancho / 2.0, 2) * (largo / 2.0)) / 1000.0;

                            double pesoEstimado = vol * 1.03;

                            string categoria = ClasificarPorPeso(pesoEstimado);

                            // CORREGIDO: actualizar contadores en hilo principal
                            Dispatcher.BeginInvoke(new Action(() => IncrementarContador(categoria)));

                            Bgr color = ObtenerColorCategoria(categoria);
                            imagen.Draw(elipse, color, 2);

                            System.Drawing.Point pos = new System.Drawing.Point(
                                (int)elipse.Center.X, (int)elipse.Center.Y - 10);
                            CvInvoke.PutText(imagen, $"{categoria}: {pesoEstimado:F1}g", pos,
                                FontFace.HersheySimplex, 0.6, new MCvScalar(255, 255, 255), 2);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ERROR ProcesarLogicaHuevo] {ex.Message}");
            }
        }

        // NUEVO: método separado para incrementar contadores de forma segura
        private void IncrementarContador(string categoria)
        {
            switch (categoria)
            {
                case "Jumbo": contadorJumbo++; break;
                case "AAA": contadorAAA++; break;
                case "AA": contadorAA++; break;
                case "A": contadorA++; break;
                case "B": contadorB++; break;
                case "C": contadorC++; break;
            }
            ActualizarResumenUI();
        }

        private string ClasificarPorPeso(double peso)
        {
            if (peso >= 73) return "Jumbo";
            else if (peso >= 67) return "AAA";
            else if (peso >= 61) return "AA";
            else if (peso >= 56) return "A";
            else if (peso >= 51) return "B";
            else return "C";
        }

        private Bgr ObtenerColorCategoria(string categoria)
        {
            switch (categoria)
            {
                case "Jumbo": return new Bgr(System.Drawing.Color.Cyan);
                case "AAA": return new Bgr(System.Drawing.Color.Blue);
                case "AA": return new Bgr(System.Drawing.Color.Yellow);
                case "A": return new Bgr(System.Drawing.Color.LimeGreen);
                case "B": return new Bgr(System.Drawing.Color.Orange);
                case "C": return new Bgr(System.Drawing.Color.Red);
                default: return new Bgr(System.Drawing.Color.White);
            }
        }

        // ─────────────────────────────────────────────────────────────
        // GUARDAR CLASIFICACIÓN AUTOMÁTICA
        // ─────────────────────────────────────────────────────────────
        private void BtnGuardarClasificacionAutomatica_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                int total = contadorJumbo + contadorAAA + contadorAA + contadorA + contadorB + contadorC;

                if (total == 0)
                {
                    MessageBox.Show(
                        "No hay datos de clasificación automática para guardar.\n" +
                        "Asegúrate de que la cámara esté conectada y detecte huevos.",
                        "Validación", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var clasificacion = new ClasificacionProduccion
                {
                    Fecha = DateTime.Now.Date,
                    Hora = DateTime.Now.TimeOfDay,
                    Recolector = "Sistema Automático",
                    TipoClasificacion = "Automática",
                    Jumbo = contadorJumbo,
                    AAA = contadorAAA,
                    AA = contadorAA,
                    A = contadorA,
                    B = contadorB,
                    C = contadorC,
                    Total = total,
                    Observaciones = "Clasificación automática por cámara y peso"
                };

                if (database.InsertarClasificacion(clasificacion))
                {
                    MessageBox.Show(
                        $"✅ Clasificación Automática Guardada\n\n" +
                        $"📅 Fecha: {clasificacion.Fecha:dd/MM/yyyy}\n" +
                        $"🕐 Hora: {clasificacion.Hora:hh\\:mm\\:ss}\n" +
                        $"🤖 Tipo: Automática\n\n" +
                        $"🥚 Total: {total} huevos\n\n" +
                        $"Jumbo: {contadorJumbo} | AAA: {contadorAAA} | AA: {contadorAA}\n" +
                        $"A: {contadorA} | B: {contadorB} | C: {contadorC}",
                        "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);

                    ReiniciarContadores();
                    ActualizarEstadisticas();
                    CargarHistorial();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al guardar: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ReiniciarContadores()
        {
            contadorJumbo = contadorAAA = contadorAA = contadorA = contadorB = contadorC = 0;
            ActualizarResumenUI();
        }

        private void ActualizarResumenUI()
        {
            Dispatcher.Invoke(() =>
            {
                lblResumenJumbo.Text = contadorJumbo.ToString();
                lblResumenAAA.Text = contadorAAA.ToString();
                lblResumenAA.Text = contadorAA.ToString();
                lblResumenA.Text = contadorA.ToString();
                lblResumenB.Text = contadorB.ToString();
                lblResumenC.Text = contadorC.ToString();

                int total = contadorJumbo + contadorAAA + contadorAA + contadorA + contadorB + contadorC;
                lblTotalResumen.Text = total.ToString();
            });
        }

        private void ActualizarEstadisticas()
        {
            Dispatcher.Invoke(() =>
            {
                int produccionHoy = database.ObtenerProduccionHoy();
                // Actualiza aquí los TextBlocks de estadísticas si los agregas al XAML
                // Ejemplo: lblProduccionHoy.Text = produccionHoy.ToString();
                System.Diagnostics.Debug.WriteLine($"[Estadísticas] Producción hoy: {produccionHoy}");
            });
        }

        private void CargarHistorial()
        {
            try
            {
                var historial = database.ObtenerClasificacionesRecientes(20);
                dgHistorial.ItemsSource = historial;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al cargar historial: {ex.Message}");
            }
        }

        // ─────────────────────────────────────────────────────────────
        // MÉTODOS DE CONVERSIÓN
        // ─────────────────────────────────────────────────────────────
        private Image<Bgr, byte> BitmapToImage(Bitmap bmp)
        {
            BitmapData data = bmp.LockBits(
                new Rectangle(0, 0, bmp.Width, bmp.Height),
                ImageLockMode.ReadOnly,
                PixelFormat.Format24bppRgb);
            Image<Bgr, byte> img = new Image<Bgr, byte>(bmp.Width, bmp.Height, data.Stride, data.Scan0);
            bmp.UnlockBits(data);
            return img.Clone();
        }

        private Bitmap ImageToBitmap(Image<Bgr, byte> img)
        {
            using (var mat = img.Mat)
            {
                Bitmap bmp = new Bitmap(mat.Width, mat.Height, PixelFormat.Format24bppRgb);
                BitmapData data = bmp.LockBits(
                    new Rectangle(0, 0, bmp.Width, bmp.Height),
                    ImageLockMode.WriteOnly,
                    PixelFormat.Format24bppRgb);
                byte[] buffer = new byte[mat.Step * mat.Rows];
                mat.CopyTo(buffer);
                Marshal.Copy(buffer, 0, data.Scan0, buffer.Length);
                bmp.UnlockBits(data);
                return bmp;
            }
        }

        private BitmapSource ConvertBitmapToBitmapSource(Bitmap bitmap)
        {
            IntPtr hBitmap = bitmap.GetHbitmap();
            try
            {
                return System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
                    hBitmap, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            }
            finally
            {
                DeleteObject(hBitmap);
            }
        }

        [DllImport("gdi32.dll")]
        public static extern bool DeleteObject(IntPtr hObject);

        // ─────────────────────────────────────────────────────────────
        // CORREGIDO: ahora espera a que el hilo se detenga completamente
        // ─────────────────────────────────────────────────────────────
        private void DesconectarCamaraUSB()
        {
            try
            {
                if (fuenteVideo != null && fuenteVideo.IsRunning)
                {
                    fuenteVideo.SignalToStop();
                    fuenteVideo.WaitForStop(); // CORREGIDO: antes faltaba WaitForStop()
                    fuenteVideo = null;
                }
                Dispatcher.Invoke(() => imgCamara.Source = null);
                camaraConectada = false;
                ActualizarEstado("📴 Cámara desconectada");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ERROR DesconectarCamaraUSB] {ex.Message}");
            }
        }

        private void BtnCapturarFoto_Click(object sender, RoutedEventArgs e)
        {
            if (!camaraConectada || imgCamara.Source == null)
            {
                MessageBox.Show("Conecta una cámara antes de capturar una foto.",
                    "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                var saveDialog = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "JPG|*.jpg",
                    FileName = $"clasificacion_{DateTime.Now:yyyyMMdd_HHmmss}"
                };

                if (saveDialog.ShowDialog() == true)
                {
                    using (FileStream fs = new FileStream(saveDialog.FileName, FileMode.Create))
                    {
                        JpegBitmapEncoder encoder = new JpegBitmapEncoder();
                        encoder.Frames.Add(BitmapFrame.Create((BitmapSource)imgCamara.Source));
                        encoder.Save(fs);
                    }
                    MessageBox.Show("✅ Foto guardada exitosamente.", "Foto guardada",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                // CORREGIDO: antes el error se ignoraba
                MessageBox.Show($"Error al guardar la foto:\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ActualizarEstado(string mensaje)
        {
            if (txtEstadoCamara != null)
                Dispatcher.Invoke(() => txtEstadoCamara.Text = mensaje);
        }

        private void dgHistorial_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
        }
    }

    public class CamaraUSB
    {
        public string Nombre { get; set; }
        public string MonikerString { get; set; }
        public override string ToString() => Nombre;
    }
}