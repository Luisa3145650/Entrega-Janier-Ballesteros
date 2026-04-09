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
using System.Linq; // <-- AGREGAR ESTA LÍNEA


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
        private int lecturasEstables = 0;

        // BASE DE DATOS
        private ClasificacionProduccionDatabase database;
        private loginavicola.Database.ClasificacionProduccionDatabase dbProduccion = new loginavicola.Database.ClasificacionProduccionDatabase();
        private double pesoGramos = 0;
        private double volumenHuevo = 0;
        private bool puedeRegistrar = true;
        private double pesoAnterior = 0;
        private bool yaRegistrado = false;
        private const int UMBRAL_ESTABILIDAD = 5;
        private List<double> historialPesos = new List<double>();

        // Contadores para clasificación automática
        private int contadorJumbo = 0;
        private Queue<double> bufferPesos = new Queue<double>();
        private const int TAMAÑO_BUFFER = 5;
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
            // 1. Instanciar la ventana
            ManualView ventana = new ManualView();
            ventana.Owner = Window.GetWindow(this);

            // 2. Abrir como Dialog y capturar la respuesta
            // Esto detiene la ejecución aquí hasta que la ventana se cierra
            if (ventana.ShowDialog() == true)
            {
                // 3. ¡ESTO ES LO VITAL! 
                // Si el DialogResult fue true, refrescamos los datos del Huila
                CargarHistorial();
                ActualizarEstadisticas();
            }
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

        // ════════════════════════════════════════════════════════
        //  MÉTODOS NUEVOS DE PROCESAMIENTO
        // ════════════════════════════════════════════════════════

        private double ObtenerPesoEstable(double nuevoPeso)
        {
            bufferPesos.Enqueue(nuevoPeso);
            if (bufferPesos.Count > TAMAÑO_BUFFER) bufferPesos.Dequeue();

            var lista = bufferPesos.ToList();
            double promedio = lista.Average();

            // Si todos los pesos en el buffer están a menos de 0.8g del promedio, es estable
            if (lista.All(p => Math.Abs(p - promedio) < 0.8))
            {
                return Math.Round(promedio, 1);
            }
            return -1; // -1 indica que aún está variando (no es estable)
        }

        private string LeerPesoMejorado(Image<Gray, byte> grisPeso)
        {
            // 1. Aumentar resolución (Fundamental para números de display)
            using (Image<Gray, byte> rescaled = grisPeso.Resize(3.0, Emgu.CV.CvEnum.Inter.Cubic))
            {
                string[] resultados = new string[2];

                // MÉTODO A: CLAHE (Ecualización adaptativa) + Otsu
                using (Image<Gray, byte> metodoA = rescaled.Clone())
                {
                    metodoA._EqualizeHist();
                    CvInvoke.Threshold(metodoA, metodoA, 0, 255, ThresholdType.BinaryInv | ThresholdType.Otsu);
                    resultados[0] = ProcesarTesseract(metodoA);
                }

                // MÉTODO B: Umbral Adaptativo Simple
                using (Image<Gray, byte> metodoB = rescaled.Clone())
                {
                    CvInvoke.AdaptiveThreshold(metodoB, metodoB, 255, AdaptiveThresholdType.GaussianC, ThresholdType.BinaryInv, 15, 7);

                    // Pequeña dilatación para unir los segmentos de los números LCD
                    using (Mat kernel = new Mat(3, 3, Emgu.CV.CvEnum.DepthType.Cv8U, 1))
                    {
                        kernel.SetTo(new MCvScalar(1));
                        CvInvoke.MorphologyEx(metodoB, metodoB, MorphOp.Close, kernel, new System.Drawing.Point(-1, -1), 1, BorderType.Default, new MCvScalar());
                    }
                    resultados[1] = ProcesarTesseract(metodoB);
                }

                // Devolver el primero que tenga sentido (que tenga números)
                foreach (var res in resultados)
                {
                    if (!string.IsNullOrEmpty(res) && res.Length >= 2) return res;
                }
            }
            return "";
        }

        private string ProcesarTesseract(Image<Gray, byte> imgProcesada)
        {
            // Opcional: Descomenta esto para ver qué lee Tesseract realmente (útil para calibrar la luz)
            // CvInvoke.Imshow("DEBUG_OCR_PESO", imgProcesada);

            using (Bitmap bmpOcr = imgProcesada.ToBitmap())
            {
                ocrEngine.SetVariable("tessedit_char_whitelist", "0123456789");
                using (var page = ocrEngine.Process(bmpOcr, PageSegMode.SingleLine))
                {
                    string raw = page.GetText().Trim();
                    return System.Text.RegularExpressions.Regex.Replace(raw, @"[^\d]", "");
                }
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

            // Definición de zonas
            Rectangle zonaHuevo = new Rectangle(80, 50, 440, 300);
            // Modifica los valores para centrar el recuadro solo en los dígitos numéricos
            Rectangle zonaPeso = new Rectangle(190, 395, 110, 45);

            try
            {
                // Dibujar guías visuales
                CvInvoke.Rectangle(imagen, zonaHuevo, new MCvScalar(0, 255, 0), 2);
                CvInvoke.Rectangle(imagen, zonaPeso, new MCvScalar(255, 0, 0), 2);

                // ══════════════════════════════════════════
                // 1. LÓGICA DEL PESO (OCR)
                // ══════════════════════════════════════════
                if (ocrEngine != null && (DateTime.Now - ultimaDeteccion).TotalMilliseconds > 500)
                {
                    using (Image<Bgr, byte> regionPeso = imagen.Copy(zonaPeso))
                    using (Image<Gray, byte> grisPeso = regionPeso.Convert<Gray, byte>())
                    {
                        string textoNumero = LeerPesoMejorado(grisPeso);

                        if (!string.IsNullOrEmpty(textoNumero) && double.TryParse(textoNumero, out double pBruto))
                        {
                            // A veces lee "500" en vez de "50.0", aplicamos lógica para ajustar
                            if (pBruto > 200 && pBruto < 2000) pBruto /= 10.0;

                            if (pBruto >= 30 && pBruto <= 120) // Rango realista de un huevo
                            {
                                double pesoConfirmado = ObtenerPesoEstable(pBruto);

                                if (pesoConfirmado != -1) // Si el buffer dice que es estable
                                {
                                    this.pesoGramos = pesoConfirmado;

                                    if (!yaRegistrado)
                                    {
                                        yaRegistrado = true;
                                        string categoriaDetectada = ClasificarHuevo(this.pesoGramos);
                                        RegistrarHuevoEnBD(this.pesoGramos, categoriaDetectada);
                                    }

                                    Dispatcher.BeginInvoke(new Action(() => {
                                        lblPesoReal.Text = $"{this.pesoGramos} g";
                                        lblCategoria.Text = ClasificarHuevo(this.pesoGramos);
                                    }));
                                }
                            }
                        }
                        else
                        {
                            // Si lee basura o números bajos, reiniciamos el ciclo para el siguiente huevo
                            if (double.TryParse(textoNumero, out double pBajo) && pBajo < 10)
                            {
                                yaRegistrado = false;
                                bufferPesos.Clear(); // Limpiamos el buffer
                            }
                        }
                    }
                }

                // ══════════════════════════════════════════
                // 2. DETECCIÓN Y VOLUMEN DEL HUEVO (HSV COLOR)
                // ══════════════════════════════════════════
                using (Image<Bgr, byte> regionHuevo = imagen.Copy(zonaHuevo))
                using (Image<Hsv, byte> hsvImg = regionHuevo.Convert<Hsv, byte>())
                using (Image<Gray, byte> mask = hsvImg.InRange(new Hsv(5, 40, 60), new Hsv(30, 255, 255))) // Filtro color huevo
                {
                    // Operaciones morfológicas para limpiar ruido
                    // Versión corregida de la creación del kernel
                    // El cast (Emgu.CV.CvEnum.ElementShape)2 o simplemente el número si la firma lo permite
                    // Usamos el valor numérico 2 que corresponde a 'Ellipse' en la librería base de OpenCV
                    // Esto evita errores de nombres de espacios de nombres que no existen
                    using (Mat kernel = CvInvoke.GetStructuringElement(0, new System.Drawing.Size(5, 5), new System.Drawing.Point(-1, -1)))
                    {
                        CvInvoke.MorphologyEx(mask, mask, Emgu.CV.CvEnum.MorphOp.Close, kernel, new System.Drawing.Point(-1, -1), 2, Emgu.CV.CvEnum.BorderType.Default, new MCvScalar());
                    }

                    using (var contornos = new Emgu.CV.Util.VectorOfVectorOfPoint())
                    {
                        CvInvoke.FindContours(mask, contornos, null, RetrType.External, ChainApproxMethod.ChainApproxSimple);

                        int mejorIndice = -1;
                        double areaMaxima = 0;

                        for (int i = 0; i < contornos.Size; i++)
                        {
                            double area = CvInvoke.ContourArea(contornos[i]);
                            if (area > 5000 && area > areaMaxima) { areaMaxima = area; mejorIndice = i; }
                        }

                        if (mejorIndice != -1 && contornos[mejorIndice].Size >= 5)
                        {
                            RotatedRect elipse = CvInvoke.FitEllipse(contornos[mejorIndice]);

                            // Calibración y Cálculo de Volumen
                            const double factorEscala = 0.033;
                            double largoReal = Math.Max(elipse.Size.Width, elipse.Size.Height) * factorEscala;
                            double anchoReal = Math.Min(elipse.Size.Width, elipse.Size.Height) * factorEscala;
                            double volumen = (4.0 / 3.0) * Math.PI * (largoReal / 2.0) * Math.Pow(anchoReal / 2.0, 2);

                            string categoria = ClasificarHuevo(this.pesoGramos);

                            RotatedRect elipseGlobal = new RotatedRect(
                                new PointF(elipse.Center.X + zonaHuevo.X, elipse.Center.Y + zonaHuevo.Y),
                                elipse.Size, elipse.Angle);

                            CvInvoke.Ellipse(imagen, elipseGlobal, new MCvScalar(0, 255, 255), 2);
                            CvInvoke.PutText(imagen, $"{categoria}: {this.pesoGramos}g",
                                new System.Drawing.Point((int)elipseGlobal.Center.X - 30, (int)elipseGlobal.Center.Y),
                                FontFace.HersheySimplex, 0.6, new MCvScalar(255, 255, 0), 2);

                            // Registro de control visual de bloqueos
                            if (this.pesoGramos >= 30)
                            {
                                if (puedeRegistrar && yaRegistrado)
                                {
                                    puedeRegistrar = false;
                                    ultimaDeteccion = DateTime.Now;

                                    // Guardado en detalle (opcional si ya usaste RegistrarHuevoEnBD arriba)
                                    // GuardarRegistroHuevo(categoria, this.pesoGramos, volumen);

                                    Dispatcher.BeginInvoke(new Action(() => {
                                        ActualizarEstado("✅ Huevo registrado. Retire para continuar.");
                                    }));
                                }
                            }
                            else if (this.pesoGramos < 10)
                            {
                                if (!puedeRegistrar)
                                {
                                    puedeRegistrar = true;
                                    Dispatcher.BeginInvoke(new Action(() => {
                                        ActualizarEstado("Ready - Coloque el siguiente huevo");
                                    }));
                                }
                            }

                            Dispatcher.BeginInvoke(new Action(() => {
                                lblVolumen.Text = $"{volumen:F1} cm³";
                            }));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ERROR ProcesarLogica] {ex.Message}");
            }
        }
        private void RegistrarHuevoEnBD(double peso, string categoria)
        {
            try
            {
                // Llamamos al método que ya tienes en ClasificacionProduccionDatabase.cs
                // El volumen lo enviamos como 0 por ahora si no lo calculas con visión
                dbProduccion.RegistrarHuevoIndividual(categoria, peso, 0);

                // Opcional: Sonido de confirmación o log
                System.Diagnostics.Debug.WriteLine($"DB: Registrado {categoria} de {peso}g");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Error al conectar con DB: " + ex.Message);
            }
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