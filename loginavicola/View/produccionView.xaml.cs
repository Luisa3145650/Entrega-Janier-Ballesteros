using loginavicola.Database;
using loginavicola.Model;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Text.Json.Serialization;
using System.Windows.Threading;
using System.Windows.Input;
using System.Threading.Tasks;
using System.Net.Http;
using System.Text.Json;

namespace loginavicola.View
{
    public partial class produccionView : UserControl
    {

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
            ActualizarEstadisticas();
            CargarHistorial();
        }

        private void InitializeComponentEventHandlers()
        {
            btnCapturarFoto.Click += BtnCapturarFoto_Click;
            btnGuardar.Click += BtnGuardarClasificacionAutomatica_Click;
            this.Loaded += ProduccionView_Loaded;

            this.Unloaded += (s, e) =>
            {
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

        private void BtnCapturarFoto_Click(object sender, RoutedEventArgs e)
        {
            // Ya no depende de "camaraConectada" (esa variable era de AForge y fue eliminada).
            // El video ahora viene siempre del último frame que entrega la API de Python.
            if (imgCamara.Source == null) return;

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
            try
            {
                // 1. Datos numéricos
                HttpResponseMessage response = await client.GetAsync("http://localhost:5001/datos-huevo");
                if (response.IsSuccessStatusCode)
                {
                    string jsonResponse = await response.Content.ReadAsStringAsync();
                    var opciones = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    DatosHuevo datos = JsonSerializer.Deserialize<DatosHuevo>(jsonResponse, opciones);

                    this.pesoGramos = datos.Peso;

                    Dispatcher.Invoke(() => {
                        lblPesoReal.Text = $"{datos.Peso} g";
                        lblCategoria.Text = string.IsNullOrEmpty(datos.Categoria) ? "-" : datos.Categoria;
                        lblVolumen.Text = $"{datos.Volumen:F1} cm³";
                    });
                }

                // 2. Frame de video procesado por Python
                byte[] frameBytes = await client.GetByteArrayAsync("http://localhost:5001/frame.jpg");
                using (var ms = new MemoryStream(frameBytes))
                {
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.StreamSource = ms;
                    bitmap.EndInit();
                    bitmap.Freeze();

                    Dispatcher.Invoke(() => {
                        imgCamara.Source = bitmap;
                        imgCamara.Opacity = 1.0; // ya no necesitas la opacidad 0.2 del XAML
                        txtEstadoCamara.Text = "Sistema Listo";
                        txtEstadoCamara.Foreground = new SolidColorBrush(Colors.LightGreen);
                    });
                }
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() => {
                    txtEstadoCamara.Text = $"⚠️ Sin conexión a la API Python: {ex.Message}";
                });
            }
        }

        // Clase Modelo para deserializar la respuesta JSON de Python
        public class DatosHuevo
        {
            [JsonPropertyName("largo")]
            public double Largo { get; set; }

            [JsonPropertyName("ancho")]
            public double Ancho { get; set; }

            [JsonPropertyName("peso")]
            public double Peso { get; set; }

            [JsonPropertyName("volumen_real")]
            public double Volumen { get; set; }

            [JsonPropertyName("categoria")]
            public string Categoria { get; set; }
        }

    }
}