// ═══════════════════════════════════════════════════════════════════
//  produccionView.xaml.cs  —  Clasificación automática de huevos
//  VERSIÓN MEJORADA: OCR Tesseract ELIMINADO → Detección 7 segmentos
// ═══════════════════════════════════════════════════════════════════

// LIBRERÍAS DE VIDEO
using AForge.Video;
using AForge.Video.DirectShow;
// LIBRERÍAS DE VISIÓN
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
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace loginavicola.View
{
    public partial class produccionView : UserControl
    {
        // ── VIDEO ────────────────────────────────────────────────────────
        private FilterInfoCollection dispositivosVideo;
        private VideoCaptureDevice fuenteVideo;
        private bool camaraConectada = false;

        // ── BASE DE DATOS ────────────────────────────────────────────────
        private ClasificacionProduccionDatabase database;
        private loginavicola.Database.ClasificacionProduccionDatabase dbProduccion
            = new loginavicola.Database.ClasificacionProduccionDatabase();

        // ── ESTADO DEL HUEVO ─────────────────────────────────────────────
        private double pesoGramos = 0;
        private bool puedeRegistrar = true;
        private bool yaRegistrado = false;

        // ── BUFFER DE ESTABILIDAD ────────────────────────────────────────
        private Queue<double> bufferPesos = new Queue<double>();
        private const int TAMAÑO_BUFFER = 5;

        // ── CONTADORES AUTOMÁTICOS ───────────────────────────────────────
        private int contadorJumbo = 0;
        private int contadorAAA = 0;
        private int contadorAA = 0;
        private int contadorA = 0;
        private int contadorB = 0;
        private int contadorC = 0;

        // ── CONTROL DE TIEMPO ────────────────────────────────────────────
        private DateTime ultimaDeteccion = DateTime.MinValue;

        // ════════════════════════════════════════════════════════════════
        //  CONSTRUCTOR
        // ════════════════════════════════════════════════════════════════
        public produccionView()
        {
            InitializeComponent();
            database = new ClasificacionProduccionDatabase();
            InitializeComponentEventHandlers();
            CargarCamarasUSB();
            ActualizarEstadisticas();
            CargarHistorial();
        }

        // ════════════════════════════════════════════════════════════════
        //  EVENTOS DE BOTONES
        // ════════════════════════════════════════════════════════════════
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
            if (ventana.ShowDialog() == true)
            {
                CargarHistorial();
                ActualizarEstadisticas();
            }
        }

        // ════════════════════════════════════════════════════════════════
        //  CÁMARAS USB
        // ════════════════════════════════════════════════════════════════
        private void CargarCamarasUSB()
        {
            try
            {
                dispositivosVideo = new FilterInfoCollection(FilterCategory.VideoInputDevice);
                cbCamaras.Items.Clear();

                System.Diagnostics.Debug.WriteLine($"[Cámaras] Total detectadas: {dispositivosVideo.Count}");

                if (dispositivosVideo.Count > 0)
                {
                    foreach (FilterInfo d in dispositivosVideo)
                    {
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
                        "• Windows bloqueando acceso (Configuración → Privacidad → Cámara)\n" +
                        "• Otra app (Teams, Zoom, OBS) está usando la cámara",
                        "Sin cámaras detectadas", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                ActualizarEstado("❌ Error al cargar cámaras");
                System.Diagnostics.Debug.WriteLine($"[ERROR CargarCamarasUSB] {ex}");
                MessageBox.Show($"Error al cargar cámaras:\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ConectarCamaraUSB()
        {
            try
            {
                if (cbCamaras.SelectedItem is not CamaraUSB cam)
                {
                    MessageBox.Show("Selecciona una cámara de la lista antes de conectar.",
                        "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (fuenteVideo != null && fuenteVideo.IsRunning)
                {
                    fuenteVideo.SignalToStop();
                    fuenteVideo.WaitForStop();
                    fuenteVideo = null;
                }

                ReiniciarContadores();

                fuenteVideo = new VideoCaptureDevice(cam.MonikerString);

                if (fuenteVideo.VideoCapabilities == null || fuenteVideo.VideoCapabilities.Length == 0)
                {
                    MessageBox.Show(
                        $"La cámara '{cam.Nombre}' no reportó capacidades de video.\n\n" +
                        "Prueba con otra cámara o reinstala el driver.",
                        "Sin capacidades de video", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                fuenteVideo.VideoResolution = fuenteVideo.VideoCapabilities[0];
                fuenteVideo.NewFrame += VideoSource_NewFrame;
                fuenteVideo.Start();

                camaraConectada = true;
                ActualizarEstado($"🔗 '{cam.Nombre}' conectada — Clasificación automática activa");
            }
            catch (Exception ex)
            {
                camaraConectada = false;
                System.Diagnostics.Debug.WriteLine($"[ERROR ConectarCamaraUSB] {ex}");
                MessageBox.Show(
                    $"No se pudo conectar la cámara:\n\n{ex.Message}\n\n" +
                    "Verifica que el dispositivo no esté siendo usado por otra aplicación.",
                    "Error al conectar", MessageBoxButton.OK, MessageBoxImage.Error);
                ActualizarEstado("❌ Error al conectar la cámara");
            }
        }

        private void DesconectarCamaraUSB()
        {
            try
            {
                if (fuenteVideo != null && fuenteVideo.IsRunning)
                {
                    fuenteVideo.SignalToStop();
                    fuenteVideo.WaitForStop();
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

        // ════════════════════════════════════════════════════════════════
        //  PROCESAMIENTO DE FRAME
        // ════════════════════════════════════════════════════════════════
        private void VideoSource_NewFrame(object sender, NewFrameEventArgs eventArgs)
        {
            try
            {
                using (Bitmap bitmap = (Bitmap)eventArgs.Frame.Clone())
                using (Image<Bgr, byte> emguImage = BitmapToImage(bitmap))
                {
                    ProcesarLogicaHuevo(emguImage);

                    using (Bitmap procesado = ImageToBitmap(emguImage))
                    {
                        var bsource = ConvertBitmapToBitmapSource(procesado);
                        Dispatcher.BeginInvoke(new Action(() => {
                            if (camaraConectada) imgCamara.Source = bsource;
                        }), DispatcherPriority.Render);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error en frame: {ex.Message}");
            }
        }

        // ════════════════════════════════════════════════════════════════
        //  LÓGICA PRINCIPAL DE DETECCIÓN
        // ════════════════════════════════════════════════════════════════
        private void ProcesarLogicaHuevo(Image<Bgr, byte> imagen)
        {
            // ── ZONAS DE DETECCIÓN ─────────────────────────────────────
            // Zona del huevo (verde): ajusta para centrar sobre el huevo
            Rectangle zonaHuevo = new Rectangle(80, 50, 440, 300);

            // ⚠️ ZONA DEL PESO (azul): AJUSTA ESTOS 4 VALORES hasta que
            //    el rectángulo azul quede EXACTAMENTE sobre los números
            int posicionX = 200;  // ← izquierda/derecha
            int posicionY = 350;  // ← arriba/abajo
            int ancho = 160;  // ← ancho del recuadro
            int alto = 70;   // ← alto del recuadro

            Rectangle zonaPeso = new Rectangle(posicionX, posicionY, ancho, alto);

            try
            {
                // Dibujar guías visuales en pantalla
                CvInvoke.Rectangle(imagen, zonaHuevo, new MCvScalar(0, 255, 0), 2);   // verde = huevo
                CvInvoke.Rectangle(imagen, zonaPeso, new MCvScalar(255, 0, 0), 2);   // azul  = peso

                // ── 1. LEER PESO (7 SEGMENTOS) ─────────────────────────
                if ((DateTime.Now - ultimaDeteccion).TotalMilliseconds > 500)
                {
                    using (Image<Bgr, byte> regionPeso = imagen.Copy(zonaPeso))
                    using (Image<Gray, byte> grisPeso = regionPeso.Convert<Gray, byte>())
                    {
                        string textoNumero = LeerPeso7Segmentos(grisPeso);

                        if (!string.IsNullOrEmpty(textoNumero))
                        {
                            // 🔥 Limitar longitud (evita cosas como 4472)
                            if (textoNumero.Length > 4)
                                textoNumero = textoNumero.Substring(0, 4);

                            // 🔥 Corregir si viene sin punto (ej: 502 → 50.2)
                            if (!textoNumero.Contains(".") && textoNumero.Length == 3)
                                textoNumero = textoNumero.Insert(2, ".");

                            if (double.TryParse(textoNumero,
                                System.Globalization.NumberStyles.Any,
                                System.Globalization.CultureInfo.InvariantCulture,
                                out double pBruto))
                            {
                                // 🔥 Corrección adicional por si sigue alto
                                if (pBruto > 200 && pBruto < 2000)
                                    pBruto /= 10.0;

                                if (pBruto >= 30 && pBruto <= 120)
                                {
                                    double pesoConfirmado = ObtenerPesoEstable(pBruto);
                                    if (pesoConfirmado != -1)
                                    {
                                        this.pesoGramos = pesoConfirmado;
                                        if (!yaRegistrado)
                                        {
                                            yaRegistrado = true;
                                            string cat = ClasificarHuevo(this.pesoGramos);
                                            RegistrarHuevoEnBD(this.pesoGramos, cat);
                                        }
                                        Dispatcher.BeginInvoke(new Action(() => {
                                            lblPesoReal.Text = $"{this.pesoGramos} g";
                                            lblCategoria.Text = ClasificarHuevo(this.pesoGramos);
                                        }));
                                    }
                                }
                            }
                        }
                        else
                        {
                            // Sin lectura válida → limpiar para el siguiente huevo
                            if (double.TryParse(textoNumero,
                                    System.Globalization.NumberStyles.Any,
                                    System.Globalization.CultureInfo.InvariantCulture,
                                    out double pBajo) && pBajo < 10)
                            {
                                yaRegistrado = false;
                                bufferPesos.Clear();
                            }
                        }
                    }
                }

                // ── 2. DETECCIÓN Y VOLUMEN DEL HUEVO (HSV) ─────────────
                using (Image<Bgr, byte> regionHuevo = imagen.Copy(zonaHuevo))
                using (Image<Hsv, byte> hsvImg = regionHuevo.Convert<Hsv, byte>())
                using (Image<Gray, byte> mask = hsvImg.InRange(
                    new Hsv(5, 40, 60), new Hsv(30, 255, 255)))
                {
                    using (Mat kernel = CvInvoke.GetStructuringElement(
                        0, new System.Drawing.Size(5, 5), new System.Drawing.Point(-1, -1)))
                    {
                        CvInvoke.MorphologyEx(mask, mask, MorphOp.Close, kernel,
                            new System.Drawing.Point(-1, -1), 2, BorderType.Default, new MCvScalar());
                    }

                    using (var contornos = new VectorOfVectorOfPoint())
                    {
                        CvInvoke.FindContours(mask, contornos, null,
                            RetrType.External, ChainApproxMethod.ChainApproxSimple);

                        int mejorIndice = -1;
                        double areaMaxima = 0;

                        for (int i = 0; i < contornos.Size; i++)
                        {
                            double area = CvInvoke.ContourArea(contornos[i]);
                            if (area > 5000 && area > areaMaxima)
                            {
                                areaMaxima = area;
                                mejorIndice = i;
                            }
                        }

                        if (mejorIndice != -1 && contornos[mejorIndice].Size >= 5)
                        {
                            RotatedRect elipse = CvInvoke.FitEllipse(contornos[mejorIndice]);

                            const double factorEscala = 0.033;
                            double largoReal = Math.Max(elipse.Size.Width, elipse.Size.Height) * factorEscala;
                            double anchoReal = Math.Min(elipse.Size.Width, elipse.Size.Height) * factorEscala;
                            double volumen = (4.0 / 3.0) * Math.PI
                                              * (largoReal / 2.0)
                                              * Math.Pow(anchoReal / 2.0, 2);

                            string categoria = ClasificarHuevo(this.pesoGramos);

                            RotatedRect elipseGlobal = new RotatedRect(
                                new PointF(elipse.Center.X + zonaHuevo.X,
                                           elipse.Center.Y + zonaHuevo.Y),
                                elipse.Size, elipse.Angle);

                            CvInvoke.Ellipse(imagen, elipseGlobal, new MCvScalar(0, 255, 255), 2);
                            CvInvoke.PutText(imagen,
                                $"{categoria}: {this.pesoGramos}g",
                                new System.Drawing.Point(
                                    (int)elipseGlobal.Center.X - 30,
                                    (int)elipseGlobal.Center.Y),
                                FontFace.HersheySimplex, 0.6,
                                new MCvScalar(255, 255, 0), 2);

                            // Control de registro
                            if (this.pesoGramos >= 30)
                            {
                                if (puedeRegistrar && yaRegistrado)
                                {
                                    puedeRegistrar = false;
                                    ultimaDeteccion = DateTime.Now;
                                    Dispatcher.BeginInvoke(new Action(() =>
                                        ActualizarEstado("✅ Huevo registrado. Retire para continuar.")));
                                }
                            }
                            else if (this.pesoGramos < 10)
                            {
                                if (!puedeRegistrar)
                                {
                                    puedeRegistrar = true;
                                    Dispatcher.BeginInvoke(new Action(() =>
                                        ActualizarEstado("Ready — Coloque el siguiente huevo")));
                                }
                            }

                            Dispatcher.BeginInvoke(new Action(() =>
                                lblVolumen.Text = $"{volumen:F1} cm³"));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ERROR ProcesarLogica] {ex.Message}");
            }
        }

        // ════════════════════════════════════════════════════════════════
        //  ★★★  DETECCIÓN DE DISPLAY 7 SEGMENTOS  ★★★
        // ════════════════════════════════════════════════════════════════

        /// <summary>
        /// Toma la región de la báscula (ya en grises), la binariza,
        /// detecta los dígitos individualmente con contornos y devuelve
        /// el texto del peso (ej: "65.3").
        /// </summary>
        private string LeerPeso7Segmentos(Image<Gray, byte> gris)
        {
            using (Image<Gray, byte> procesada = gris.Clone())
            {
                CvInvoke.GaussianBlur(procesada, procesada,
                    new System.Drawing.Size(3, 3), 0);

                CvInvoke.Threshold(procesada, procesada, 0, 255,
                    ThresholdType.BinaryInv | ThresholdType.Otsu);

                try
                {
                    List<(Rectangle bbox, Image<Gray, byte> img)> digitosDetectados
                        = DetectarDigitosConContornos(procesada);

                    if (digitosDetectados.Count == 0)
                        return LeerPorDivision(procesada);

                    string resultado = "";
                    double anchoPromedio = digitosDetectados.Average(d => d.Item1.Width);

                    foreach (var item in digitosDetectados)
                    {
                        Rectangle bbox = item.Item1;
                        var digitoImg = item.Item2;

                        if (bbox.Width < anchoPromedio * 0.3)
                        {
                            resultado += ".";
                            digitoImg.Dispose();
                            continue;
                        }

                        int digito = DetectarDigito7Segmentos(digitoImg);
                        if (digito >= 0)
                            resultado += digito.ToString();

                        digitoImg.Dispose();
                    }

                    System.Diagnostics.Debug.WriteLine($"[7SEG] Leído: '{resultado}'");
                    return resultado;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[ERROR 7SEG] {ex.Message}");
                }

                return ""; // ← fallback final obligatorio
            }
        }

        /// <summary>
        /// Detecta cada dígito de la pantalla usando contornos (nivel PRO).
        /// Retorna lista ordenada de izquierda a derecha.
        /// </summary>
        private List<(Rectangle bbox, Image<Gray, byte> img)> DetectarDigitosConContornos(
            Image<Gray, byte> binarizada)
        {
            var resultados = new List<(Rectangle, Image<Gray, byte>)>();

            try
            {
                using (var contornos = new VectorOfVectorOfPoint())
                {
                    // Clonar para no modificar la original
                    using (Image<Gray, byte> copia = binarizada.Clone())
                    {
                        CvInvoke.FindContours(copia, contornos, null,
                            RetrType.External, ChainApproxMethod.ChainApproxSimple);
                    }

                    int alturaMinima = binarizada.Height / 3; // Descartar ruido pequeño

                    for (int i = 0; i < contornos.Size; i++)
                    {
                        Rectangle bbox = CvInvoke.BoundingRectangle(contornos[i]);

                        // Filtrar por tamaño: solo contornos que sean dígitos reales
                        if (bbox.Height >= alturaMinima && bbox.Width >= 5)
                        {
                            // Asegurar que el bbox no se salga de la imagen
                            bbox = Rectangle.Intersect(bbox,
                                new Rectangle(0, 0, binarizada.Width, binarizada.Height));

                            if (bbox.Width > 0 && bbox.Height > 0)
                            {
                                resultados.Add((bbox, binarizada.Copy(bbox)));
                            }
                        }
                    }
                }

                // Ordenar de izquierda a derecha (vital para leer "65.3" correctamente)
                resultados.Sort((a, b) => a.Item1.X.CompareTo(b.Item1.X));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ERROR DetectarDigitos] {ex.Message}");
            }

            return resultados;
        }

        /// <summary>
        /// Plan B: divide la imagen en N partes iguales y lee cada una.
        /// Útil si los contornos fallan.
        /// </summary>
        private string LeerPorDivision(Image<Gray, byte> binarizada)
        {
            // ⚠️ AJUSTA este número según tu báscula:
            // 3 → muestra "65.2"  (3 dígitos + punto implícito)
            // 4 → muestra "65.21" (4 dígitos)
            const int NUM_DIGITOS = 3;

            int anchoDigito = binarizada.Width / NUM_DIGITOS;
            string resultado = "";

            for (int i = 0; i < NUM_DIGITOS; i++)
            {
                Rectangle zona = new Rectangle(
                    i * anchoDigito, 0, anchoDigito, binarizada.Height);

                using (var digitoImg = binarizada.Copy(zona))
                {
                    int digito = DetectarDigito7Segmentos(digitoImg);
                    if (digito >= 0)
                        resultado += digito.ToString();
                }
            }

            return resultado;
        }

        /// <summary>
        /// Analiza los 7 segmentos de UN dígito y retorna el número (0-9).
        /// Retorna -1 si no reconoce el patrón.
        ///
        ///   ─ a ─
        ///  |     |
        ///  f     b
        ///  |     |
        ///   ─ g ─
        ///  |     |
        ///  e     c
        ///  |     |
        ///   ─ d ─
        /// </summary>
        private int DetectarDigito7Segmentos(Image<Gray, byte> img)
        {
            int w = img.Width;
            int h = img.Height;

            if (w < 5 || h < 10) return -1; // imagen demasiado pequeña

            // ── DEFINIR ZONAS DE CADA SEGMENTO ───────────────────────
            // Cada Rectangle cubre el área donde debería estar ese segmento
            Rectangle[] zonas = new Rectangle[]
            {
                // a — horizontal superior
                new Rectangle(w/4,       0,          w/2, h/6),
                // f — vertical superior izquierda
                new Rectangle(0,         h/6,        w/4, h/3),
                // b — vertical superior derecha
                new Rectangle(3*w/4,     h/6,        w/4, h/3),
                // g — horizontal central
                new Rectangle(w/4,       h/2 - h/12, w/2, h/6),
                // e — vertical inferior izquierda
                new Rectangle(0,         h/2,        w/4, h/3),
                // c — vertical inferior derecha
                new Rectangle(3*w/4,     h/2,        w/4, h/3),
                // d — horizontal inferior
                new Rectangle(w/4,       5*h/6,      w/2, h/6)
            };

            int[] encendidos = new int[7];

            for (int i = 0; i < 7; i++)
            {
                // Asegurar que la zona no se salga de la imagen
                Rectangle zona = Rectangle.Intersect(zonas[i],
                    new Rectangle(0, 0, w, h));

                if (zona.Width <= 0 || zona.Height <= 0)
                {
                    encendidos[i] = 0;
                    continue;
                }

                using (var roi = img.Copy(zona))
                {
                    double promedio = CvInvoke.Mean(roi).V0;

                    // ⚠️ UMBRAL: si falla ajusta entre 80 y 160
                    // Fondo negro + dígitos blancos → promedio > 120 = segmento activo
                    // Fondo claro + dígitos oscuros → invierte la lógica
                    encendidos[i] = promedio > 90 ? 1 : 0;
                }
            }

            return MapearSegmentos(encendidos);
        }

        /// <summary>
        /// Convierte el patrón binario de 7 segmentos al dígito correspondiente.
        /// Orden: a, f, b, g, e, c, d
        /// </summary>
        private int MapearSegmentos(int[] s)
        {
            string clave = string.Join("", s);

            // Tabla completa de los 10 dígitos
            var mapa = new Dictionary<string, int>
            {
                { "1111110", 0 },
                { "0110000", 1 },
                { "1101101", 2 },
                { "1111001", 3 },
                { "0110011", 4 },
                { "1011011", 5 },
                { "1011111", 6 },
                { "1110000", 7 },
                { "1111111", 8 },
                { "1111011", 9 }
            };

            if (mapa.TryGetValue(clave, out int digito))
                return digito;

            // Si no coincide exactamente, buscar el más parecido (tolerancia a errores)
            return BuscarDigitoMasCercano(s);
        }

        /// <summary>
        /// Si el patrón no coincide exactamente, busca el dígito con
        /// menor diferencia de segmentos (tolerancia a reflejos/ruido).
        /// </summary>
        private int BuscarDigitoMasCercano(int[] s)
        {
            var patrones = new Dictionary<string, int>
            {
                { "1111110", 0 }, { "0110000", 1 }, { "1101101", 2 },
                { "1111001", 3 }, { "0110011", 4 }, { "1011011", 5 },
                { "1011111", 6 }, { "1110000", 7 }, { "1111111", 8 },
                { "1111011", 9 }
            };

            int mejorDigito = -1;
            int menorDiferencia = 4; // Tolerancia máxima: 3 segmentos diferentes

            foreach (var par in patrones)
            {
                int diferencias = 0;
                for (int i = 0; i < 7; i++)
                    if (par.Key[i] - '0' != s[i]) diferencias++;

                if (diferencias < menorDiferencia)
                {
                    menorDiferencia = diferencias;
                    mejorDigito = par.Value;
                }
            }

            System.Diagnostics.Debug.WriteLine(
                $"[7SEG Fuzzy] Patrón: {string.Join("", s)} → {mejorDigito} (diff={menorDiferencia})");

            return mejorDigito;
        }

        // ════════════════════════════════════════════════════════════════
        //  ESTABILIDAD DEL PESO
        // ════════════════════════════════════════════════════════════════
        private double ObtenerPesoEstable(double nuevoPeso)
        {
            bufferPesos.Enqueue(nuevoPeso);
            if (bufferPesos.Count > TAMAÑO_BUFFER) bufferPesos.Dequeue();

            var lista = bufferPesos.ToList();
            double prom = lista.Average();

            // Estable si todas las lecturas están a menos de 0.8g del promedio
            if (lista.All(p => Math.Abs(p - prom) < 0.8))
                return Math.Round(prom, 1);

            return -1;
        }

        // ════════════════════════════════════════════════════════════════
        //  CLASIFICACIÓN
        // ════════════════════════════════════════════════════════════════
        private string ClasificarHuevo(double peso)
        {
            if (peso >= 78) return "Jumbo";
            if (peso >= 67) return "AAA";
            if (peso >= 60) return "AA";
            if (peso >= 53) return "A";
            if (peso >= 46) return "B";
            return "C";
        }

        // ════════════════════════════════════════════════════════════════
        //  BASE DE DATOS
        // ════════════════════════════════════════════════════════════════
        private void RegistrarHuevoEnBD(double peso, string categoria)
        {
            try
            {
                dbProduccion.RegistrarHuevoIndividual(categoria, peso, 0);
                System.Diagnostics.Debug.WriteLine($"DB: Registrado {categoria} de {peso}g");

                Dispatcher.BeginInvoke(new Action(() => IncrementarContador(categoria)));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Error al conectar con DB: " + ex.Message);
            }
        }

        private void GuardarRegistroHuevo(string categoria, double peso, double volumen)
        {
            try
            {
                using (var conexion = new Microsoft.Data.Sqlite.SqliteConnection(
                    "Data Source=avicola.db"))
                {
                    conexion.Open();
                    string query =
                        "INSERT INTO detalle_produccion " +
                        "(fecha, hora, categoria, peso, volumen, id_usuario) " +
                        "VALUES (@fecha, @hora, @cat, @peso, @vol, @user)";

                    var cmd = new Microsoft.Data.Sqlite.SqliteCommand(query, conexion);
                    cmd.Parameters.AddWithValue("@fecha", DateTime.Now.ToString("yyyy-MM-dd"));
                    cmd.Parameters.AddWithValue("@hora", DateTime.Now.ToString("HH:mm:ss"));
                    cmd.Parameters.AddWithValue("@cat", categoria);
                    cmd.Parameters.AddWithValue("@peso", peso);
                    cmd.Parameters.AddWithValue("@vol", volumen);
                    cmd.Parameters.AddWithValue("@user", 1);
                    cmd.ExecuteNonQuery();
                }

                Dispatcher.Invoke(() => IncrementarContador(categoria));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Error al guardar en SQLite: " + ex.Message);
            }
        }

        // ════════════════════════════════════════════════════════════════
        //  CONTADORES Y UI
        // ════════════════════════════════════════════════════════════════
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

                int total = contadorJumbo + contadorAAA + contadorAA
                          + contadorA + contadorB + contadorC;
                lblTotalResumen.Text = total.ToString();
            });
        }

        private void ReiniciarContadores()
        {
            contadorJumbo = contadorAAA = contadorAA =
            contadorA = contadorB = contadorC = 0;
            ActualizarResumenUI();
        }

        private void BtnGuardarClasificacionAutomatica_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                int total = contadorJumbo + contadorAAA + contadorAA
                          + contadorA + contadorB + contadorC;

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
                        $"Fecha: {clasificacion.Fecha:dd/MM/yyyy}\n" +
                        $"Hora:  {clasificacion.Hora:hh\\:mm\\:ss}\n\n" +
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

        private void ActualizarEstadisticas()
        {
            Dispatcher.Invoke(() =>
            {
                int produccionHoy = database.ObtenerProduccionHoy();
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

        // ════════════════════════════════════════════════════════════════
        //  COLOR POR CATEGORÍA (para uso visual futuro)
        // ════════════════════════════════════════════════════════════════
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

        // ════════════════════════════════════════════════════════════════
        //  CONVERSIÓN DE IMÁGENES
        // ════════════════════════════════════════════════════════════════
        private Image<Bgr, byte> BitmapToImage(Bitmap bmp)
        {
            BitmapData data = bmp.LockBits(
                new Rectangle(0, 0, bmp.Width, bmp.Height),
                ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);

            Image<Bgr, byte> img = new Image<Bgr, byte>(
                bmp.Width, bmp.Height, data.Stride, data.Scan0);

            bmp.UnlockBits(data);
            return img.Clone();
        }

        private Bitmap ImageToBitmap(Image<Bgr, byte> img)
        {
            using (var mat = img.Mat)
            {
                Bitmap bmp = new Bitmap(mat.Width, mat.Height,
                    PixelFormat.Format24bppRgb);

                BitmapData data = bmp.LockBits(
                    new Rectangle(0, 0, bmp.Width, bmp.Height),
                    ImageLockMode.WriteOnly, PixelFormat.Format24bppRgb);

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
                    hBitmap, IntPtr.Zero, Int32Rect.Empty,
                    BitmapSizeOptions.FromEmptyOptions());
                bsource.Freeze(); // Permite pasar entre hilos
                return bsource;
            }
            finally
            {
                DeleteObject(hBitmap); // Libera memoria GDI
            }
        }

        [DllImport("gdi32.dll")]
        public static extern bool DeleteObject(IntPtr hObject);

        // ════════════════════════════════════════════════════════════════
        //  CAPTURA DE FOTO
        // ════════════════════════════════════════════════════════════════
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
                        var encoder = new JpegBitmapEncoder();
                        encoder.Frames.Add(BitmapFrame.Create((BitmapSource)imgCamara.Source));
                        encoder.Save(fs);
                    }
                    MessageBox.Show("✅ Foto guardada exitosamente.", "Foto guardada",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al guardar la foto:\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ════════════════════════════════════════════════════════════════
        //  HELPERS
        // ════════════════════════════════════════════════════════════════
        private void ActualizarEstado(string mensaje)
        {
            if (txtEstadoCamara != null)
                Dispatcher.Invoke(() => txtEstadoCamara.Text = mensaje);
        }

        private void dgHistorial_SelectionChanged(object sender, SelectionChangedEventArgs e) { }
    }

    // ════════════════════════════════════════════════════════════════════
    //  MODELO AUXILIAR
    // ════════════════════════════════════════════════════════════════════
    public class CamaraUSB
    {
        public string Nombre { get; set; }
        public string MonikerString { get; set; }
        public override string ToString() => Nombre;
    }
}