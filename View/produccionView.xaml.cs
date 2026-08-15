using loginavicola.Database;
using loginavicola.Model;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;


namespace loginavicola.View
{
    public partial class produccionView : UserControl
    {
        private bool leyendoBascula = false;
        private bool camaraActiva = false;

        // Semáforo para controlar y evitar encolamiento de peticiones HTTP del streaming
        private readonly SemaphoreSlim semaphoreCamara = new SemaphoreSlim(1, 1);

        private ClasificacionProduccionDatabase database;
        private DetalleClasificacionDatabase dbDetalle = new DetalleClasificacionDatabase();
        private LoteDatabase dbLote = new LoteDatabase();

        private double pesoGramos = 0;
        private DateTime ultimaDeteccion = DateTime.MinValue;

        private int contadorJumbo = 0;
        private int contadorAAA = 0;
        private int contadorAA = 0;
        private int contadorA = 0;
        private int contadorB = 0;
        private int contadorC = 0;

        private DateTime horaInicioLote = DateTime.Now;

        private int idLoteSeleccionado = 0;
        private string nombreLoteSeleccionado = "";

        private static readonly HttpClient client = new HttpClient();
        private static readonly JsonSerializerOptions jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        public produccionView()
        {
            InitializeComponent();
            database = new ClasificacionProduccionDatabase();

            leyendoBascula = true;

            Task.Run(async () => {
                while (leyendoBascula)
                {
                    await ConsultarDatosHuevo();
                    await Task.Delay(300);
                }
            });

            horaInicioLote = DateTime.Now;
            InitializeComponentEventHandlers();
            CargarLotes();
            ActualizarEstadisticas();
            CargarHistorial();
        }

        private async void IniciarCamara()
        {
            if (camaraActiva) return;

            try
            {
                var response = await client.PostAsync("http://localhost:5001/iniciar_camara", null);
                if (response.IsSuccessStatusCode)
                {
                    camaraActiva = true;

                    Task.Run(async () => {
                        while (leyendoBascula && camaraActiva)
                        {
                            await ConsultarFrameCamara();
                            await Task.Delay(80);
                        }
                    });
                }
                else
                {
                    Dispatcher.Invoke(() => {
                        txtEstadoCamara.Text = $"⚠️ Servidor respondió: {response.StatusCode}";
                        txtEstadoCamara.Foreground = new SolidColorBrush(Colors.Crimson);
                    });
                }
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() => {
                    txtEstadoCamara.Text = $"⚠️ Error al conectar cámara: {ex.Message}";
                    txtEstadoCamara.Foreground = new SolidColorBrush(Colors.Crimson);
                });
            }
        }

        private async void DetenerCamara()
        {
            camaraActiva = false;

            try
            {
                await client.PostAsync("http://localhost:5001/detener_camara", null);
            }
            catch { }

            Dispatcher.Invoke(() => {
                imgCamara.Source = null;
                txtEstadoCamara.Text = "📷 Cámara Desconectada";
                txtEstadoCamara.Foreground = new SolidColorBrush(Colors.Orange);
            });
        }

        private void BtnConectarCamara_Click(object sender, RoutedEventArgs e)
        {
            IniciarCamara();
        }

        private void BtnDesconectarCamara_Click(object sender, RoutedEventArgs e)
        {
            DetenerCamara();
        }

        private void InitializeComponentEventHandlers()
        {
            btnGuardar.Click += BtnGuardarClasificacionAutomatica_Click;
            btnClasificacionManual.Click += btnClasificacionManual_Click;
            btnRefrescarLotes.Click += (s, e) => CargarLotes();
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
        }

        private void ProduccionView_Unloaded(object sender, RoutedEventArgs e)
        {
            leyendoBascula = false;
            DetenerCamara();

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
                e.Handled = true; // Previene comportamiento predeterminado del foco
                RegistrarHuevoManual();
            }
        }

        private class LoteComboItem
        {
            public int IdLote { get; set; }
            public string Display { get; set; }
        }

        private void CargarLotes()
        {
            try
            {
                var lotes = dbLote.ObtenerTodosLosLotes();
                var itemsCombo = lotes.Select(l => new LoteComboItem
                {
                    IdLote = l.IdLote,
                    Display = $"Lote #{l.IdLote} - {l.Raza} ({l.FechaIncorporacion:dd/MM/yyyy})"
                }).ToList();

                cmbLote.DisplayMemberPath = "Display";
                cmbLote.SelectedValuePath = "IdLote";
                cmbLote.ItemsSource = itemsCombo;

                if (itemsCombo.Count == 0)
                {
                    txtEstadoLote.Text = "⚠️ No hay lotes registrados. Crea un lote primero.";
                }
                else
                {
                    txtEstadoLote.Text = "⚠️ Selecciona un lote para comenzar a clasificar";
                }
                txtEstadoLote.Foreground = new SolidColorBrush(Colors.Crimson);

                idLoteSeleccionado = 0;
                btnGuardar.IsEnabled = false;
                btnClasificacionManual.IsEnabled = false;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error cargando lotes: " + ex.Message);
            }
        }

        private void CmbLote_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cmbLote.SelectedItem is LoteComboItem item)
            {
                idLoteSeleccionado = item.IdLote;
                nombreLoteSeleccionado = item.Display;

                txtEstadoLote.Text = $"✅ {item.Display} seleccionado";
                txtEstadoLote.Foreground = new SolidColorBrush(Colors.ForestGreen);

                btnGuardar.IsEnabled = true;
                btnClasificacionManual.IsEnabled = true;

                contadorJumbo = 0;
                contadorAAA = 0;
                contadorAA = 0;
                contadorA = 0;
                contadorB = 0;
                contadorC = 0;
                horaInicioLote = DateTime.Now;
                ActualizarResumenUI();
            }
            else
            {
                idLoteSeleccionado = 0;
                nombreLoteSeleccionado = "";
                btnGuardar.IsEnabled = false;
                btnClasificacionManual.IsEnabled = false;
            }
        }

        private void RegistrarHuevoManual()
        {
            if (idLoteSeleccionado <= 0)
            {
                ActualizarEstado("⚠️ Selecciona un lote antes de clasificar.");
                return;
            }

            if (this.pesoGramos <= 0)
            {
                ActualizarEstado("⚠️ Báscula en 0. Pon un huevo.");
                return;
            }

            if ((DateTime.Now - ultimaDeteccion).TotalSeconds < 1.2) return;

            ultimaDeteccion = DateTime.Now;
            string categoria = ClasificarHuevo(this.pesoGramos);
            ContarHuevoEnMemoria(categoria);
            ActualizarEstado($"✅ Contabilizado: {this.pesoGramos}g - {categoria}");
        }

        private void btnClasificacionManual_Click(object sender, RoutedEventArgs e)
        {
            if (idLoteSeleccionado <= 0)
            {
                MessageBox.Show("Selecciona un lote antes de abrir la clasificación manual.", "Lote requerido", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            ManualView ventana = new ManualView("Invitado", idLoteSeleccionado, nombreLoteSeleccionado);
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

        private void ContarHuevoEnMemoria(string categoria)
        {
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
                lblCategoria.Text = categoria;
                ActualizarResumenUI();
            }));
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

        private void CargarHistorial()
        {
            try
            {
                var datos = database.ObtenerHistorial();
                dgHistorial.ItemsSource = datos;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error cargando historial: " + ex.Message);
            }
        }

        private void BtnGuardarClasificacionAutomatica_Click(object sender, RoutedEventArgs e)
        {
            if (idLoteSeleccionado <= 0)
            {
                MessageBox.Show("Selecciona un lote antes de guardar.", "Lote requerido", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            int total = contadorJumbo + contadorAAA + contadorAA + contadorA + contadorB + contadorC;

            if (total <= 0)
            {
                MessageBox.Show("No hay huevos contabilizados en este lote todavía.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                var clasificacion = new ClasificacionProduccion
                {
                    IdLote = idLoteSeleccionado,
                    Fecha = DateTime.Now.Date,
                    HoraInicio = horaInicioLote.ToString("HH:mm:ss"),
                    Recolector = "Sistema Visión",
                    TipoClasificacion = "Automática",
                    Jumbo = contadorJumbo,
                    AAA = contadorAAA,
                    AA = contadorAA,
                    A = contadorA,
                    B = contadorB,
                    C = contadorC,
                    Total = total,
                    Observaciones = "Clasificación automática por báscula/cámara"
                };

                if (database.InsertarClasificacion(clasificacion))
                {
                    MessageBox.Show($"✅ Sesión terminada y guardada.\nLote: {nombreLoteSeleccionado}\nTotal: {total} huevos", "Información", MessageBoxButton.OK, MessageBoxImage.Information);

                    contadorJumbo = 0;
                    contadorAAA = 0;
                    contadorAA = 0;
                    contadorA = 0;
                    contadorB = 0;
                    contadorC = 0;
                    horaInicioLote = DateTime.Now;
                    lblCategoria.Text = "-";
                    ActualizarResumenUI();

                    CargarHistorial();
                    ActualizarEstadisticas();
                }
                else
                {
                    MessageBox.Show("No se pudo guardar el lote. Intenta de nuevo.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al guardar el lote: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ActualizarEstado(string msg)
        {
            Dispatcher.InvokeAsync(() => txtEstadoCamara.Text = msg);
        }

        private async Task ConsultarDatosHuevo()
        {
            try
            {
                HttpResponseMessage response = await client.GetAsync("http://localhost:5001/datos-huevo");
                if (response.IsSuccessStatusCode)
                {
                    string jsonResponse = await response.Content.ReadAsStringAsync();
                    DatosHuevo datos = JsonSerializer.Deserialize<DatosHuevo>(jsonResponse, jsonOptions);

                    this.pesoGramos = datos.Peso;

                    Dispatcher.Invoke(() => {
                        lblPesoReal.Text = $"{datos.Peso} g";
                        lblVolumen.Text = $"{datos.Volumen:F1} cm³";
                    });
                }
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() => {
                    txtEstadoCamara.Text = $"⚠️ Sin conexión a API datos: {ex.Message}";
                });
            }
        }

        private async Task ConsultarFrameCamara()
        {
            if (!camaraActiva || !semaphoreCamara.Wait(0)) return;

            try
            {
                byte[] frameBytes = await client.GetByteArrayAsync("http://localhost:5001/frame.jpg");

                if (frameBytes == null || frameBytes.Length == 0) return;

                using (var ms = new MemoryStream(frameBytes))
                {
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.StreamSource = ms;
                    bitmap.EndInit();
                    bitmap.Freeze();

                    Dispatcher.Invoke(() => {
                        if (camaraActiva)
                        {
                            imgCamara.Source = bitmap;
                            imgCamara.Opacity = 1.0;
                            txtEstadoCamara.Text = "Sistema Listo";
                            txtEstadoCamara.Foreground = new SolidColorBrush(Colors.LightGreen);
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() => {
                    if (camaraActiva)
                    {
                        txtEstadoCamara.Text = $"⚠️ Error streaming: {ex.Message}";
                    }
                });
            }
            finally
            {
                semaphoreCamara.Release();
            }
        }

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