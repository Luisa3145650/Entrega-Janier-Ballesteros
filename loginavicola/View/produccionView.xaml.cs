// LIBRERÍAS DE VIDEO
using AForge.Video;
using AForge.Video.DirectShow;
// LIBRERÍAS DE VISIÓN
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using loginavicola.Database;
using loginavicola.Model;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Point = System.Drawing.Point;
using Tesseract; // <-- AGREGAR ESTA LÍNEA
using System.Drawing.Drawing2D;


namespace loginavicola.View
{
    public partial class produccionView : UserControl
    {
        private FilterInfoCollection dispositivosVideo;
        private VideoCaptureDevice fuenteVideo;
        private bool camaraConectada = false;
        private double factorConversion = 0.1;
        private int totalHuevos = 0;
        private int huevosBuenos = 0;
        private int lotesActivos = 1;

        // BASE DE DATOS
        private ClasificacionProduccionDatabase database;
        private double pesoGramos = 0;

        // Contadores para clasificación automática
        private int contadorJumbo = 0;
        private int contadorAAA = 0;
        private int contadorAA = 0;
        private int contadorA = 0;
        private int contadorB = 0;
        private int contadorC = 0;
        // --- AGREGAR ESTAS LÍNEAS AQUÍ ABAJO DE LA CLASE ---
        private FilterInfoCollection misDispositivos;
        private VideoCaptureDevice miWebCam;

        // Variables para evitar duplicados al registrar
        private DateTime ultimaDeteccion = DateTime.MinValue;
        private int intervaloDeteccionMs = 2000; // Espera 2 segundos entre huevos
        private object volumenCm3;
        private TesseractEngine ocrEngine; // Esto resuelve los primeros dos errores

        public produccionView()
        {
            InitializeComponent();
            database = new ClasificacionProduccionDatabase();
            InitializeComponentEventHandlers();
            CargarCamarasUSB();
            ActualizarEstadisticas();
            CargarHistorial();

            // Inicialización optimizada:
            ocrEngine = new TesseractEngine(@"./tessdata", "eng", EngineMode.Default);
            // IMPORTANTE: Solo reconocer dígitos para evitar errores
            ocrEngine.SetVariable("tessedit_char_whitelist", "0123456789");
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
            ManualView ventana = new ManualView();
            ventana.Owner = Window.GetWindow(this);
            ventana.ShowDialog();
        }

        // CORREGIDO: ahora muestra errores detallados al cargar cámaras
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


        // CORREGIDO: ahora tiene try-catch y valida que haya item seleccionado

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
                // El 'using' asegura que el frame se destruya después de usarse, liberando RAM
                using (Bitmap bitmap = (Bitmap)eventArgs.Frame.Clone())
                {
                    using (Image<Bgr, byte> emguImage = BitmapToImage(bitmap))
                    {
                        ProcesarLogicaHuevo(emguImage);

                        using (Bitmap procesado = ImageToBitmap(emguImage))
                        {
                            var bsource = ConvertBitmapToBitmapSource(procesado);

                            // Actualiza la UI de forma asíncrona pero segura
                            Dispatcher.BeginInvoke(new Action(() => {
                                if (camaraConectada) imgCamara.Source = bsource;
                            }), DispatcherPriority.Render);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error en frame: {ex.Message}");
            }
        }

        private void ProcesarLogicaHuevo(Image<Bgr, byte> imagen)
        {
            // Ajustamos la zona del peso un poquito más arriba y a la izquierda para centrar el "64"
            Rectangle zonaHuevo = new Rectangle(100, 50, 440, 300);
            Rectangle zonaPeso = new Rectangle(150, 415, 140, 60);

            try
            {
                // Dibujamos las zonas para referencia visual
                CvInvoke.Rectangle(imagen, zonaHuevo, new MCvScalar(0, 255, 0), 2);
                CvInvoke.Rectangle(imagen, zonaPeso, new MCvScalar(255, 0, 0), 2);

                // --- 1. LÓGICA DEL PESO (OCR) ---
                // Solo procesamos el peso cada 500ms para no saturar la PC
                if (zonaPeso.Right <= imagen.Width && zonaPeso.Bottom <= imagen.Height && zonaPeso.X >= 0 && zonaPeso.Y >= 0)
                {
                    if (ocrEngine != null && (DateTime.Now - ultimaDeteccion).TotalMilliseconds > 500)
                    {
                        using (Image<Bgr, byte> regionPeso = imagen.Copy(zonaPeso))
                        using (Image<Gray, byte> grisPeso = regionPeso.Convert<Gray, byte>())
                        {
                            grisPeso._EqualizeHist();
                            // Agrandamos (Interpolación Cúbica es ideal para OCR)
                            using (Image<Gray, byte> reescalada = grisPeso.Resize(2.0, Emgu.CV.CvEnum.Inter.Cubic))
                            {

                                CvInvoke.AdaptiveThreshold(reescalada, reescalada, 255,
                                      AdaptiveThresholdType.MeanC, ThresholdType.BinaryInv, 11, 2);

                                
                                // Filtro Otsu + BinaryInv: 
                                // Convierte números negros en fondo gris a blanco puro sobre negro (mejor para Tesseract)
                                CvInvoke.Threshold(reescalada, reescalada, 0, 255, ThresholdType.BinaryInv | ThresholdType.Otsu);

                                // Ventana de depuración (Si no ves el 65 aquí, el OCR no lo leerá)
                                CvInvoke.Imshow("DEBUG_PESO", reescalada);

                                using (Bitmap bmpOcr = reescalada.ToBitmap())
                                using (var page = ocrEngine.Process(bmpOcr))
                                {
                                    string texto = page.GetText().Trim();
                                    string soloNumeros = System.Text.RegularExpressions.Regex.Replace(texto, @"[^\d]", "");

                                    if (!string.IsNullOrEmpty(soloNumeros) && double.TryParse(soloNumeros, out double p))
                                    {
                                        // Filtro de peso lógico para huevos (entre 30g y 120g)
                                        if (p >= 30 && p <= 120)
                                        {
                                            this.pesoGramos = p;
                                            System.Diagnostics.Debug.WriteLine($"LECTURA EXITOSA: {p}g");

                                            // Actualizar la interfaz (UI)
                                            Dispatcher.BeginInvoke(new Action(() => {
                                                lblPesoReal.Text = $"{p} g";
                                                lblCategoria.Text = ClasificarHuevo(p);
                                            }));
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("⚠️ Alerta: El cuadro de la báscula está fuera de los límites de la cámara.");
                }

                // --- 2. LÓGICA DE DETECCIÓN DEL HUEVO ---
                using (Image<Bgr, byte> regionHuevo = imagen.Copy(zonaHuevo))
                using (UMat gris = new UMat())
                using (UMat binaria = new UMat())
                {
                    CvInvoke.CvtColor(regionHuevo, gris, ColorConversion.Bgr2Gray);
                    CvInvoke.GaussianBlur(gris, gris, new System.Drawing.Size(5, 5), 1.5);
                    CvInvoke.Threshold(gris, binaria, 135, 255, ThresholdType.BinaryInv);

                    using (var contornos = new Emgu.CV.Util.VectorOfVectorOfPoint())
                    {
                        CvInvoke.FindContours(binaria, contornos, null, RetrType.External, ChainApproxMethod.ChainApproxSimple);

                        int mejorIndice = -1;
                        double areaMaxima = 0;

                        for (int i = 0; i < contornos.Size; i++)
                        {
                            double area = CvInvoke.ContourArea(contornos[i]);
                            if (area > 3500 && area > areaMaxima)
                            {
                                areaMaxima = area;
                                mejorIndice = i;
                            }
                        }

                        if (mejorIndice != -1)
                        {
                            RotatedRect elipse = CvInvoke.FitEllipse(contornos[mejorIndice]);

                            double factorEscala = 0.035;
                            double largo = Math.Max(elipse.Size.Width, elipse.Size.Height) * factorEscala;
                            double ancho = Math.Min(elipse.Size.Width, elipse.Size.Height) * factorEscala;
                            double volumen = (4.0 / 3.0) * Math.PI * (largo / 2.0) * Math.Pow(ancho / 2.0, 2);

                            string categoria = ClasificarHuevo(this.pesoGramos);

                            // Dibujo de la elipse y peso en la pantalla
                            RotatedRect elipseGlobal = new RotatedRect(
                                new PointF(elipse.Center.X + zonaHuevo.X, elipse.Center.Y + zonaHuevo.Y),
                                elipse.Size, elipse.Angle);

                            CvInvoke.Ellipse(imagen, elipseGlobal, new MCvScalar(0, 255, 255), 2);
                            CvInvoke.PutText(imagen, $"{categoria}: {this.pesoGramos}g",
                                new Point((int)elipseGlobal.Center.X - 30, (int)elipseGlobal.Center.Y),
                                FontFace.HersheySimplex, 0.6, new MCvScalar(255, 255, 0), 2);

                            // Actualizar UI
                            Dispatcher.BeginInvoke(new Action(() => {
                                lblPesoReal.Text = $"{this.pesoGramos} g";
                                lblCategoria.Text = categoria;
                                lblVolumen.Text = $"{volumen:F1} cm³";
                            }));

                            // Registro automático: Si el peso es real y el huevo está presente
                            if (this.pesoGramos >= 30 && (DateTime.Now - ultimaDeteccion).TotalMilliseconds > 2500)
                                     {
                                         ultimaDeteccion = DateTime.Now;
                                         
                                         // Llamada a tu base de datos corregida
                                         var db = new ClasificacionProduccionDatabase();
                                         db.RegistrarHuevoIndividual(categoria, this.pesoGramos, volumen);
                                         
                                         System.Diagnostics.Debug.WriteLine($"✅ REGISTRADO EN BD: {categoria} - {this.pesoGramos}g");
                                     }
                        }
                    }
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Error en ProcesarLogica: {ex.Message}"); }
        }

        // RESUELVE EL ERROR: 'ClasificarHuevo' no existe
        private string ClasificarHuevo(double peso)
        {
            if (peso >= 78) return "Jumbo";
            if (peso >= 67) return "AAA";
            if (peso >= 60) return "AA";
            if (peso >= 53) return "A";
            if (peso >= 46) return "B";
            return "C";
        }

        // RESUELVE EL ERROR: 'GuardarRegistroHuevo' no existe
        private void GuardarRegistroHuevo(string categoria, double peso, double volumen)
        {
            try
            {
                // Usamos la ruta de tu base de datos SQLite
                using (var conexion = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=avicola.db"))
                {
                    conexion.Open();
                    string query = "INSERT INTO detalle_produccion (fecha, hora, categoria, peso, volumen, id_usuario) " +
                                   "VALUES (@fecha, @hora, @cat, @peso, @vol, @user)";

                    var cmd = new Microsoft.Data.Sqlite.SqliteCommand(query, conexion);
                    cmd.Parameters.AddWithValue("@fecha", DateTime.Now.ToString("yyyy-MM-dd"));
                    cmd.Parameters.AddWithValue("@hora", DateTime.Now.ToString("HH:mm:ss"));
                    cmd.Parameters.AddWithValue("@cat", categoria);
                    cmd.Parameters.AddWithValue("@peso", peso);
                    cmd.Parameters.AddWithValue("@vol", volumen);
                    cmd.Parameters.AddWithValue("@user", 1); // ID de usuario por defecto

                    cmd.ExecuteNonQuery();
                }

                // Actualizamos los contadores visuales
                Dispatcher.Invoke(() => {
                    IncrementarContador(categoria);
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Error al guardar en SQLite: " + ex.Message);
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

        // GUARDAR CLASIFICACIÓN AUTOMÁTICA
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
                        $"Clasificación Automática Guardada\n\n" +
                        $"Fecha: {clasificacion.Fecha:dd/MM/yyyy}\n" +
                        $"Hora: {clasificacion.Hora:hh\\:mm\\:ss}\n" +
                        $"Tipo: Automática\n\n" +
                        $"Total: {total} huevos\n\n" +
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

        // MÉTODOS DE CONVERSIÓN
        private Image<Bgr, byte> BitmapToImage(Bitmap bmp)
        {
            // Bloquea los bits para una conversión segura y rápida
            BitmapData data = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
            Image<Bgr, byte> img = new Image<Bgr, byte>(bmp.Width, bmp.Height, data.Stride, data.Scan0);
            bmp.UnlockBits(data);
            return img.Clone(); // Retorna una copia limpia
        }

        private Bitmap ImageToBitmap(Image<Bgr, byte> img)
        {
            using (var mat = img.Mat)
            {
                Bitmap bmp = new Bitmap(mat.Width, mat.Height, PixelFormat.Format24bppRgb);
                BitmapData data = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.WriteOnly, PixelFormat.Format24bppRgb);

                int bytes = mat.Step * mat.Rows;
                byte[] buffer = new byte[bytes];
                mat.CopyTo(buffer);
                Marshal.Copy(buffer, 0, data.Scan0, bytes);

                bmp.UnlockBits(data);
                return bmp;
            }
        }

        private BitmapSource ConvertBitmapToBitmapSource(Bitmap bitmap)
        {
            IntPtr hBitmap = bitmap.GetHbitmap();
            try
            {
                var bsource = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
                    hBitmap, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                bsource.Freeze(); // ESTO ES VITAL: Permite que la imagen pase del hilo de la cámara a la UI
                return bsource;
            }
            finally
            {
                DeleteObject(hBitmap); // Libera memoria GDI
            }
        }

        [DllImport("gdi32.dll")]
        public static extern bool DeleteObject(IntPtr hObject);

        // CORREGIDO: ahora espera a que el hilo se detenga completamente
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