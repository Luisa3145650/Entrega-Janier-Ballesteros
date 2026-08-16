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
        private DetalleClasificacionDatabase dbDetalle = new DetalleClasificacionDatabase();
        private LoteDatabase dbLote = new LoteDatabase();

        private double pesoGramos = 0;
        private DateTime ultimaDeteccion = DateTime.MinValue;

        // Contadores en memoria del lote actual. Solo se guardan en la base de
        // datos cuando el usuario presiona "Guardar" (un único INSERT consolidado).
        private int contadorJumbo = 0;
        private int contadorAAA = 0;
        private int contadorAA = 0;
        private int contadorA = 0;
        private int contadorB = 0;
        private int contadorC = 0;

        // Marca cuándo empezó el lote actual, para guardar HoraInicio correctamente
        private DateTime horaInicioLote = DateTime.Now;

        // Lote seleccionado obligatoriamente antes de poder clasificar (manual o automático)
        private int idLoteSeleccionado = 0;
        private string nombreLoteSeleccionado = "";

        // Cliente HTTP reutilizable para consultar la API de Python
        private static readonly HttpClient client = new HttpClient();

        public produccionView()
        {
            InitializeComponent();
            database = new ClasificacionProduccionDatabase();

            // INTEGRACIÓN: dos hilos independientes -- uno para datos (peso/categoria/volumen,
            // no necesita ser muy frecuente) y otro para el video (necesita refrescar rapido
            // para que no se vea "lento" al mover el huevo). Si comparten un solo loop
            // secuencial, el video queda atado a la latencia de la consulta de datos.
            leyendoBascula = true;

            Task.Run(async () => {
                while (leyendoBascula)
                {
                    await ConsultarDatosHuevo();
                    await Task.Delay(300); // el peso/categoria no necesita refrescar tan rapido
                }
            });

            Task.Run(async () => {
                while (leyendoBascula)
                {
                    await ConsultarFrameCamara();
                    await Task.Delay(80); // video mas fluido (~10-12 fps aprox)
                }
            });

            horaInicioLote = DateTime.Now;
            InitializeComponentEventHandlers();
            CargarLotes();
            ActualizarEstadisticas();
            CargarHistorial();
        }

        private void InitializeComponentEventHandlers()
        {
            btnCapturarFoto.Click += BtnCapturarFoto_Click;
            btnGuardar.Click += BtnGuardarClasificacionAutomatica_Click;
            btnClasificacionManual.Click += btnClasificacionManual_Click;
            btnRefrescarLotes.Click += (s, e) => CargarLotes();
            cmbLote.SelectionChanged += CmbLote_SelectionChanged;
            btnConectarHardware.Click += async (s, e) => await ConectarHardwareAsync();
            btnRefrescarHardware.Click += async (s, e) => await CargarDispositivosDisponiblesAsync();
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

            _ = CargarConfiguracionYDispositivosAsync();
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Space)
            {
                RegistrarHuevoManual();
                e.Handled = true;
            }
        }

        // =====================================================================
        // SELECCIÓN DE LOTE (obligatoria antes de clasificar, manual o automático)
        // =====================================================================

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

                // Se perdió la selección al recargar; bloquea clasificación hasta elegir de nuevo
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

                // Al cambiar de lote se reinicia el conteo en memoria del lote anterior
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

        /// <summary>
        /// Solo acumula el conteo en memoria y refresca la UI. NO escribe en la base
        /// de datos: el registro del lote se guarda una única vez al presionar "Guardar".
        /// </summary>
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

        /// <summary>
        /// Único punto donde se escribe en la base de datos para la clasificación
        /// automática: toma los contadores acumulados en memoria durante el lote
        /// y los guarda como UN SOLO registro consolidado, vinculado al lote elegido.
        /// </summary>
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

                    // Reinicia los contadores para el siguiente lote
                    contadorJumbo = 0;
                    contadorAAA = 0;
                    contadorAA = 0;
                    contadorA = 0;
                    contadorB = 0;
                    contadorC = 0;
                    horaInicioLote = DateTime.Now;
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

        // =====================================================================
        // NUEVA INTEGRACIÓN: PETICIONES HTTP A LA API FLASK EN PYTHON 🚀
        // =====================================================================

        private async Task ConsultarDatosHuevo()
        {
            try
            {
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
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() => {
                    txtEstadoCamara.Text = $"⚠️ Sin conexión a la API Python (datos): {ex.Message}";
                });
            }
        }

        private async Task ConsultarFrameCamara()
        {
            try
            {
                byte[] frameBytes = await client.GetByteArrayAsync("http://localhost:5001/frame.jpg");

                // Si Python aun no tiene un frame listo, /frame.jpg devuelve cuerpo vacio (204).
                // Evita intentar decodificar un arreglo vacio como imagen (eso causaba el
                // "NotSupportedException: no se encontro componente de procesamiento de imagenes").
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
                        imgCamara.Source = bitmap;
                        imgCamara.Opacity = 1.0;
                        txtEstadoCamara.Text = "Sistema Listo";
                        txtEstadoCamara.Foreground = new SolidColorBrush(Colors.LightGreen);
                    });
                }
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() => {
                    txtEstadoCamara.Text = $"⚠️ Sin conexión a la API Python (video): {ex.Message}";
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

        #region CONFIGURACIÓN DE HARDWARE (BÁSCULA Y CÁMARA)

        public class PuertoInfo
        {
            [JsonPropertyName("puerto")]
            public string Puerto { get; set; }

            [JsonPropertyName("descripcion")]
            public string Descripcion { get; set; }
        }

        public class CamaraInfo
        {
            [JsonPropertyName("id")]
            public int Id { get; set; }

            [JsonPropertyName("nombre")]
            public string Nombre { get; set; }
        }

        public class DispositivosResponse
        {
            [JsonPropertyName("puertos")]
            public List<PuertoInfo> Puertos { get; set; }

            [JsonPropertyName("camaras")]
            public List<CamaraInfo> Camaras { get; set; }
        }

        public class EstadoConfiguracionResponse
        {
            [JsonPropertyName("puerto_bascula")]
            public string PuertoBascula { get; set; }

            [JsonPropertyName("camara_index")]
            public int CamaraIndex { get; set; }

            [JsonPropertyName("camara_nombre")]
            public string CamaraNombre { get; set; }

            [JsonPropertyName("configurado")]
            public bool Configurado { get; set; }

            [JsonPropertyName("conectado")]
            public bool Conectado { get; set; }
        }

        private void ActualizarBadgeEstadoHardware(bool conectado, string mensaje)
        {
            Dispatcher.Invoke(() =>
            {
                txtEstadoConexionHardware.Text = mensaje;
                if (mensaje.Contains("Buscando") || mensaje.Contains("Conectando"))
                {
                    brdEstadoConexion.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FEF3C7"));
                    txtEstadoConexionHardware.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D97706"));
                }
                else if (conectado)
                {
                    brdEstadoConexion.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DCFCE7"));
                    txtEstadoConexionHardware.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#16A34A"));
                }
                else
                {
                    brdEstadoConexion.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FEE2E2"));
                    txtEstadoConexionHardware.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DC2626"));
                }
            });
        }

        private async Task CargarConfiguracionYDispositivosAsync()
        {
            ActualizarBadgeEstadoHardware(false, "⏳ Buscando...");
            btnConectarHardware.IsEnabled = false;

            try
            {
                await CargarDispositivosDisponiblesAsync();

                string json = await client.GetStringAsync("http://localhost:5001/estado-configuracion");
                var config = JsonSerializer.Deserialize<EstadoConfiguracionResponse>(json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (config != null && config.Configurado)
                {
                    if (!string.IsNullOrEmpty(config.PuertoBascula))
                    {
                        cmbPuertos.SelectedValue = config.PuertoBascula;
                    }

                    cmbCamaras.SelectedValue = config.CamaraIndex;

                    ActualizarBadgeEstadoHardware(config.Conectado, config.Conectado ? "🟢 Conectado" : "🔴 Desconectado");
                }
                else
                {
                    ActualizarBadgeEstadoHardware(false, "🔴 Desconectado");
                    Dispatcher.Invoke(() =>
                    {
                        txtEstadoCamara.Text = "Hardware no configurado. Elige cámara y puerto arriba.";
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error al consultar estado de configuración: {ex.Message}");
                ActualizarBadgeEstadoHardware(false, "🔴 Desconectado");
            }
            finally
            {
                btnConectarHardware.IsEnabled = true;
            }
        }

        private async Task CargarDispositivosDisponiblesAsync()
        {
            try
            {
                string json = await client.GetStringAsync("http://localhost:5001/dispositivos-disponibles");
                var datos = JsonSerializer.Deserialize<DispositivosResponse>(json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                var puertos = datos?.Puertos ?? new List<PuertoInfo>();
                var camaras = datos?.Camaras ?? new List<CamaraInfo>();

                cmbPuertos.ItemsSource = puertos;
                cmbPuertos.DisplayMemberPath = "Descripcion";
                cmbPuertos.SelectedValuePath = "Puerto";

                cmbCamaras.ItemsSource = camaras;
                cmbCamaras.DisplayMemberPath = "Nombre";
                cmbCamaras.SelectedValuePath = "Id";

                if (cmbPuertos.SelectedIndex < 0 && cmbPuertos.Items.Count > 0)
                    cmbPuertos.SelectedIndex = 0;

                if (cmbCamaras.SelectedIndex < 0 && cmbCamaras.Items.Count > 0)
                    cmbCamaras.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error al cargar dispositivos disponibles: {ex.Message}");
            }
        }

        private async Task ConectarHardwareAsync()
        {
            if (cmbPuertos.SelectedValue is not string puerto || string.IsNullOrEmpty(puerto))
            {
                MessageBox.Show("Por favor selecciona un puerto para la báscula.", "Falta Información", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (cmbCamaras.SelectedValue == null)
            {
                MessageBox.Show("Por favor selecciona una cámara.", "Falta Información", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            int camaraId = Convert.ToInt32(cmbCamaras.SelectedValue);

            btnConectarHardware.IsEnabled = false;
            ActualizarBadgeEstadoHardware(false, "⏳ Conectando...");

            try
            {
                var payload = JsonSerializer.Serialize(new
                {
                    puerto_bascula = puerto,
                    camara_index = camaraId
                });

                var contenido = new StringContent(payload, System.Text.Encoding.UTF8, "application/json");
                var respuesta = await client.PostAsync("http://localhost:5001/guardar-configuracion", contenido);

                if (respuesta.IsSuccessStatusCode)
                {
                    string jsonResult = await respuesta.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(jsonResult);
                    bool conectado = doc.RootElement.TryGetProperty("conectado", out var prop) && prop.GetBoolean();

                    ActualizarBadgeEstadoHardware(conectado, conectado ? "🟢 Conectado" : "🔴 Desconectado");
                }
                else
                {
                    ActualizarBadgeEstadoHardware(false, "🔴 Error al Conectar");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al conectar hardware: {ex.Message}", "Error de Conexión", MessageBoxButton.OK, MessageBoxImage.Error);
                ActualizarBadgeEstadoHardware(false, "🔴 Desconectado");
            }
            finally
            {
                btnConectarHardware.IsEnabled = true;
            }
        }

        #endregion

    }
}