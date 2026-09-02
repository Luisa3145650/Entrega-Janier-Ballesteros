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
using loginavicola.ViewModel;

namespace loginavicola.View
{
    public partial class produccionView : UserControl
    {

        private produccionViewModel viewModel;
        private bool leyendoBascula = false;
        private ClasificacionProduccionDatabase database;
        private loginavicola.Database.ClasificacionProduccionDatabase dbProduccion = new loginavicola.Database.ClasificacionProduccionDatabase();
        private DetalleClasificacionDatabase dbDetalle = new DetalleClasificacionDatabase();
        private LoteDatabase dbLote = new LoteDatabase();

        private double pesoGramos = 0;
        private bool huevoDetectado = false;
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
            viewModel = new produccionViewModel();
            this.DataContext = viewModel;
            database = new ClasificacionProduccionDatabase();

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

        // =====================================================================
        // SELECCIÓN DE LOTE (obligatoria antes de clasificar, manual o automático)
        // =====================================================================

        private void CargarLotes()
        {
            try
            {
                var lotes = dbLote.ObtenerTodosLosLotes();
                var itemsCombo = lotes.Select(l => new loginavicola.ViewModel.LoteComboItem
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

            if (this.pesoGramos <= 2.0)
            {
                ActualizarEstado("⚠️ Báscula sin peso (> 2.0g). Pon un huevo.");
                return;
            }

            if (!this.huevoDetectado)
            {
                ActualizarEstado("⚠️ Objeto no reconocido por el sistema de visión.");
                MessageBox.Show("El sistema de visión no reconoce el objeto en la báscula como un huevo válido. Por favor, retire el objeto.", "Alerta de Seguridad", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if ((DateTime.Now - ultimaDeteccion).TotalSeconds < 0.6) return;

            ultimaDeteccion = DateTime.Now;
            string categoria = ClasificarHuevo(this.pesoGramos);
            ContarHuevoEnMemoria(categoria);
            ActualizarEstado($"✅ Contabilizado: {this.pesoGramos:F1}g - {categoria}");
        }

        private void btnClasificacionManual_Click(object sender, RoutedEventArgs e)
        {
            if (idLoteSeleccionado <= 0)
            {
                MessageBox.Show("Selecciona un lote antes de abrir la clasificación manual.", "Lote requerido", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string usuarioActivo = UserSession.UsuarioActual?.NombreCompleto 
                ?? UserSession.UsuarioActual?.Username 
                ?? (UserSession.EsVisitante ? "Visitante" : "Operador");

            ManualView ventana = new ManualView(usuarioActivo, idLoteSeleccionado, nombreLoteSeleccionado);
            ventana.Owner = Window.GetWindow(this);
            if (ventana.ShowDialog() == true)
            {
                CargarHistorial();
                ActualizarEstadisticas();
            }
        }

        private string ClasificarHuevo(double peso)
        {
            if (peso >= 78.0) return "Jumbo";
            if (peso >= 67.0) return "AAA";
            if (peso >= 60.0) return "AA";
            if (peso >= 53.0) return "A";
            if (peso >= 45.0) return "B";
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
                _ = viewModel?.CargarHistorialAsync();
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
                if (this.pesoGramos > 2.0)
                {
                    string cat = ClasificarHuevo(this.pesoGramos);
                    ContarHuevoEnMemoria(cat);
                    total = 1;
                }
                else
                {
                    MessageBox.Show("No hay huevos contabilizados en este lote todavía. Presiona ESPACIO para clasificar o coloca un huevo con peso en la báscula.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
            }

            try
            {
                var clasificacion = new ClasificacionProduccion
                {
                    IdLote = idLoteSeleccionado,
                    Fecha = DateTime.Now.Date,
                    HoraInicio = horaInicioLote.ToString("HH:mm:ss"),
                    Recolector = UserSession.UsuarioActual?.NombreCompleto 
                        ?? UserSession.UsuarioActual?.Username 
                        ?? (UserSession.EsVisitante ? "Visitante" : "Operador"),
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
                    this.huevoDetectado = datos.HuevoDetectado || datos.EsValido;

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
    }
}