using AForge.Video;
using AForge.Video.DirectShow;
using System.IO.Ports;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using Emgu.CV.Util;
using loginavicola.Database;
using loginavicola.Model;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Windows.Input;
using System.Threading.Tasks;
using System.Net.Http;
using System.Text.Json;

namespace loginavicola.View
{
    public partial class produccionView : UserControl
    {
        private FilterInfoCollection dispositivosVideo;
        private VideoCaptureDevice fuenteVideo;
        private bool camaraConectada = false;
        private bool procesandoFrame = false;

        private bool leyendoBascula = false;
        private ClasificacionProduccionDatabase database;
        private loginavicola.Database.ClasificacionProduccionDatabase dbProduccion = new loginavicola.Database.ClasificacionProduccionDatabase();

        private double pesoGramos = 0;
        private DateTime ultimaDeteccion = DateTime.MinValue;

        private int contadorJumbo = 0;
        private int contadorAAA = 0;
        private int contadorAA = 0;
        private int contadorA = 0;
        private int contadorB = 0;
        private int contadorC = 0;

        // Cliente HTTP reutilizable para consultar la API de Python
        private static readonly HttpClient client = new HttpClient();

        public produccionView()
        {
            InitializeComponent();
            database = new ClasificacionProduccionDatabase();

            // INTEGRACIÓN: Hilo en segundo plano que consulta constantemente a Python (Flask)
            Task.Run(async () => {
                leyendoBascula = true;
                while (leyendoBascula)
                {
                    await ConsultarHardwarePython();
                    await Task.Delay(300); // Consulta el peso y volumen cada 300ms
                }
            });

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

            this.Loaded += ProduccionView_Loaded;

            this.Unloaded += (s, e) =>
            {
                DesconectarCamaraUSB();
                leyendoBascula = false;
                Window window = Window.GetWindow(this);
                if (window != null) window.PreviewKeyDown -= Window_PreviewKeyDown;
            };
        }

        private void ProduccionView_Loaded(object sender, RoutedEventArgs e)
        {
            Window window = Window.GetWindow(this);
            if (window != null) window.PreviewKeyDown += Window_PreviewKeyDown;
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Space)
            {
                RegistrarHuevoManual();
                e.Handled = true;
            }
        }

        private void RegistrarHuevoManual()
        {
            if (this.pesoGramos <= 0)
            {
                ActualizarEstado("⚠️ Báscula en 0. Pon un huevo.");
                return;
            }

            if ((DateTime.Now - ultimaDeteccion).TotalSeconds < 1.2) return;

            ultimaDeteccion = DateTime.Now;
            string categoria = ClasificarHuevo(this.pesoGramos);
            RegistrarHuevoEnBD(this.pesoGramos, categoria);
            ActualizarEstado($"✅ Guardado: {this.pesoGramos}g - {categoria}");
        }

        private void btnClasificacionManual_Click(object sender, RoutedEventArgs e)
        {
            ManualView ventana = new ManualView("Invitado");
            ventana.Owner = Window.GetWindow(this);
            if (ventana.ShowDialog() == true)
            {
                CargarHistorial();
                ActualizarEstadisticas();
            }
        }

        private void CargarCamarasUSB()
        {
            try
            {
                dispositivosVideo = new FilterInfoCollection(FilterCategory.VideoInputDevice);
                cbCamaras.Items.Clear();
                if (dispositivosVideo.Count > 0)
                {
                    foreach (FilterInfo d in dispositivosVideo) cbCamaras.Items.Add(new CamaraUSB { Nombre = d.Name, MonikerString = d.MonikerString });
                    cbCamaras.SelectedIndex = 0;
                    ActualizarEstado($"✅ {dispositivosVideo.Count} cámaras detectadas");
                }
                else ActualizarEstado("⚠️ No hay cámaras");
            }
            catch (Exception ex) { ActualizarEstado($"❌ Error buscando cámaras: {ex.Message}"); }
        }

        private void ConectarCamaraUSB()
        {
            try
            {
                if (cbCamaras.SelectedItem is not CamaraUSB cam) return;
                if (fuenteVideo != null && fuenteVideo.IsRunning) { fuenteVideo.SignalToStop(); fuenteVideo = null; }

                fuenteVideo = new VideoCaptureDevice(cam.MonikerString);
                fuenteVideo.NewFrame += VideoSource_NewFrame;
                fuenteVideo.Start();
                camaraConectada = true;
                ActualizarEstado("🔗 Conectando cámara... Si se queda en negro, revisa la privacidad de Windows.");
            }
            catch (Exception ex) { ActualizarEstado($"❌ Error al encender cámara: {ex.Message}"); }
        }

        private void DesconectarCamaraUSB()
        {
            if (fuenteVideo != null) { fuenteVideo.SignalToStop(); fuenteVideo = null; }
            Dispatcher.Invoke(() => imgCamara.Source = null);
            camaraConectada = false;
        }

        private void VideoSource_NewFrame(object sender, NewFrameEventArgs eventArgs)
        {
            if (procesandoFrame) return;
            procesandoFrame = true;

            try
            {
                using (Bitmap bitmap = (Bitmap)eventArgs.Frame.Clone())
                using (Image<Bgr, byte> emguImage = BitmapToImage(bitmap))
                {
                    ProcesarLogicaHuevo(emguImage);
                    using (Bitmap procesado = ImageToBitmap(emguImage))
                    {
                        var bsource = ConvertBitmapToBitmapSource(procesado);
                        Dispatcher.BeginInvoke(new Action(() => { if (camaraConectada) imgCamara.Source = bsource; }), DispatcherPriority.Render);
                    }
                }
            }
            catch (Exception ex)
            {
                Dispatcher.BeginInvoke(new Action(() => {
                    txtEstadoCamara.Text = $"❌ Error en IA de cámara: {ex.Message}";
                }));
            }
            finally
            {
                procesandoFrame = false;
            }
        }

        private void ProcesarLogicaHuevo(Image<Bgr, byte> imagen)
        {
            Rectangle zonaHuevo = new Rectangle(80, 50, 440, 300);

            try
            {
                CvInvoke.Rectangle(imagen, zonaHuevo, new MCvScalar(0, 255, 0), 2);

                using (Image<Bgr, byte> regionHuevo = imagen.Copy(zonaHuevo))
                using (Image<Hsv, byte> hsvImg = regionHuevo.Convert<Hsv, byte>())
                using (Image<Gray, byte> mask = hsvImg.InRange(new Hsv(5, 40, 60), new Hsv(30, 255, 255)))
                {
                    using (Mat kernel = CvInvoke.GetStructuringElement(0, new System.Drawing.Size(5, 5), new System.Drawing.Point(-1, -1)))
                    {
                        CvInvoke.MorphologyEx(mask, mask, MorphOp.Close, kernel, new System.Drawing.Point(-1, -1), 2, BorderType.Default, new MCvScalar());
                    }

                    using (var contornos = new VectorOfVectorOfPoint())
                    {
                        CvInvoke.FindContours(mask, contornos, null, RetrType.External, ChainApproxMethod.ChainApproxSimple);
                        double areaMax = 0; int idx = -1;
                        for (int i = 0; i < contornos.Size; i++)
                        {
                            double a = CvInvoke.ContourArea(contornos[i]);
                            if (a > 5000 && a > areaMax) { areaMax = a; idx = i; }
                        }
                        if (idx != -1)
                        {
                            RotatedRect elipse = CvInvoke.FitEllipse(contornos[idx]);
                            double largoHuevo = Math.Max(elipse.Size.Width, elipse.Size.Height) * 0.033;
                            double anchoHuevo = Math.Min(elipse.Size.Width, elipse.Size.Height) * 0.033;
                            double vol = (4.0 / 3.0) * Math.PI * (largoHuevo / 2.0) * Math.Pow(anchoHuevo / 2.0, 2);

                            RotatedRect elipseG = new RotatedRect(new PointF(elipse.Center.X + zonaHuevo.X, elipse.Center.Y + zonaHuevo.Y), elipse.Size, elipse.Angle);
                            CvInvoke.Ellipse(imagen, elipseG, new MCvScalar(0, 255, 255), 2);

                            if (this.pesoGramos > 0)
                            {
                                string categoria = ClasificarHuevo(this.pesoGramos);
                                CvInvoke.PutText(imagen, $"{categoria}: {this.pesoGramos}g", new System.Drawing.Point((int)elipseG.Center.X - 30, (int)elipseG.Center.Y), FontFace.HersheySimplex, 0.6, new MCvScalar(255, 255, 0), 2);
                            }

                            Dispatcher.BeginInvoke(new Action(() => lblVolumen.Text = $"{vol:F1} cm³"));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Fallo en OpenCV (¿Falta Emgu.CV.runtime.windows?) " + ex.Message);
            }
        }

        private string ClasificarHuevo(double peso)
        {
            if (peso >= 78) return "Jumbo";
            if (peso >= 67) return "AAA";
            if (peso >= 60) return "AA";
            if (peso >= 53) return "A";
            if (peso >= 46) return "B";
            return "C";
        }

        private void RegistrarHuevoEnBD(double peso, string categoria)
        {
            try
            {
                dbProduccion.RegistrarHuevoIndividual(categoria, peso, 0);
                Dispatcher.BeginInvoke(new Action(() => {
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
                }));
            }
            catch { }
        }

        private void ActualizarResumenUI()
        {
            Dispatcher.Invoke(() => {
                lblResumenJumbo.Text = contadorJumbo.ToString();
                lblResumenAAA.Text = contadorAAA.ToString();
                lblResumenAA.Text = contadorAA.ToString();
                lblResumenA.Text = contadorA.ToString();
                lblResumenB.Text = contadorB.ToString();
                lblResumenC.Text = contadorC.ToString();
                lblTotalResumen.Text = (contadorJumbo + contadorAAA + contadorAA + contadorA + contadorB + contadorC).ToString();
            });
        }

        private void ActualizarEstadisticas() { Task.Run(() => database.ObtenerProduccionHoy()); }

        private void CargarHistorial() { try { dgHistorial.ItemsSource = database.ObtenerHistorial(); } catch { } }

        private Image<Bgr, byte> BitmapToImage(Bitmap bmp)
        {
            BitmapData data = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
            Image<Bgr, byte> img = new Image<Bgr, byte>(bmp.Width, bmp.Height, data.Stride, data.Scan0);
            bmp.UnlockBits(data); return img.Clone();
        }

        private Bitmap ImageToBitmap(Image<Bgr, byte> img)
        {
            using (var mat = img.Mat)
            {
                Bitmap bmp = new Bitmap(mat.Width, mat.Height, PixelFormat.Format24bppRgb);
                BitmapData data = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.WriteOnly, PixelFormat.Format24bppRgb);
                int bytes = mat.Step * mat.Rows; byte[] buffer = new byte[bytes];
                mat.CopyTo(buffer); Marshal.Copy(buffer, 0, data.Scan0, bytes);
                bmp.UnlockBits(data); return bmp;
            }
        }

        private BitmapSource ConvertBitmapToBitmapSource(Bitmap bitmap)
        {
            using (MemoryStream memory = new MemoryStream())
            {
                bitmap.Save(memory, ImageFormat.Bmp);
                memory.Position = 0;
                BitmapImage bitmapImage = new BitmapImage();
                bitmapImage.BeginInit();
                bitmapImage.StreamSource = memory;
                bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                bitmapImage.EndInit();
                bitmapImage.Freeze();
                return bitmapImage;
            }
        }

        [DllImport("gdi32.dll")] public static extern bool DeleteObject(IntPtr hObject);

        private void BtnCapturarFoto_Click(object sender, RoutedEventArgs e)
        {
            if (!camaraConectada || imgCamara.Source == null) return;
            var saveDialog = new Microsoft.Win32.SaveFileDialog { Filter = "JPG|*.jpg", FileName = $"huevo_{DateTime.Now:ss}" };
            if (saveDialog.ShowDialog() == true)
            {
                using (FileStream fs = new FileStream(saveDialog.FileName, FileMode.Create))
                {
                    var encoder = new JpegBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create((BitmapSource)imgCamara.Source));
                    encoder.Save(fs);
                }
            }
        }

        private void BtnGuardarClasificacionAutomatica_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("✅ Sesión terminada y guardada.");
        }

        private void ActualizarEstado(string msg)
        {
            Dispatcher.InvokeAsync(() => txtEstadoCamara.Text = msg);
        }

        // =====================================================================
        // NUEVA INTEGRACIÓN: PETICIONES HTTP A LA API FLASK EN PYTHON 🚀
        // =====================================================================
        private async Task ConsultarHardwarePython()
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

                    // Mapeo directo y seguro a los elementos visuales en el hilo de la UI
                    Dispatcher.Invoke(() => {
                        this.pesoGramos = datos.Peso;
                        lblPesoReal.Text = $"{datos.Peso} g";
                        lblCategoria.Text = string.IsNullOrEmpty(datos.Categoria) ? "-" : datos.Categoria;
                        lblVolumen.Text = $"{datos.Volumen:F1} cm³";
                    });
                }
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() => {
                    txtEstadoCamara.Text = $"⚠️ Buscando API Python... {ex.Message}";
                });
            }
        }

        // Clase Modelo para deserializar la respuesta JSON de Python
        public class DatosHuevo
        {
            public double Peso { get; set; }
            public string Categoria { get; set; }
            public double Volumen { get; set; }
        }

        public class CamaraUSB { public string Nombre { get; set; } public string MonikerString { get; set; } public override string ToString() => Nombre; }
    }
}