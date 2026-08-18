using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using loginavicola.Database;
using loginavicola.Helpers;
using loginavicola.Model;

namespace loginavicola.ViewModel
{
    public class LoteComboItem
    {
        public int IdLote { get; set; }
        public string Display { get; set; }
    }

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



    public partial class produccionViewModel : ObservableObject
    {
        // ──────────────────────────────────────────────────────────────
        // Usuario en sesión actual
        // ──────────────────────────────────────────────────────────────
        [ObservableProperty]
        private string usuarioActualNombre = UserSession.UsuarioActual?.NombreCompleto 
            ?? UserSession.UsuarioActual?.Username 
            ?? "Invitado";

        // ──────────────────────────────────────────────────────────────
        // Lecturas del sensor / API Python
        // ──────────────────────────────────────────────────────────────
        [ObservableProperty]
        private double pesoGramos = 0;

        [ObservableProperty]
        private string pesoRealTexto = "0.0 g";

        [ObservableProperty]
        private string categoriaActual = "-";

        [ObservableProperty]
        private string volumenTexto = "-";

        // ──────────────────────────────────────────────────────────────
        // Contadores en memoria de la sesión activa
        // ──────────────────────────────────────────────────────────────
        [ObservableProperty]
        private int contadorJumbo = 0;

        [ObservableProperty]
        private int contadorAAA = 0;

        [ObservableProperty]
        private int contadorAA = 0;

        [ObservableProperty]
        private int contadorA = 0;

        [ObservableProperty]
        private int contadorB = 0;

        [ObservableProperty]
        private int contadorC = 0;

        [ObservableProperty]
        private int totalResumen = 0;

        // ──────────────────────────────────────────────────────────────
        // Selección y estado de Lote
        // ──────────────────────────────────────────────────────────────
        [ObservableProperty]
        private ObservableCollection<LoteComboItem> lotesDisponibles = new();

        [ObservableProperty]
        private LoteComboItem loteSeleccionado;

        [ObservableProperty]
        private int idLoteSeleccionado = 0;

        [ObservableProperty]
        private string nombreLoteSeleccionado = "";

        [ObservableProperty]
        private string textoEstadoLote = "⚠️ Selecciona un lote para comenzar a clasificar";

        [ObservableProperty]
        private Brush colorEstadoLote = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DC2626"));

        // ──────────────────────────────────────────────────────────────
        // Dispositivos y estado de Hardware
        // ──────────────────────────────────────────────────────────────
        [ObservableProperty]
        private ObservableCollection<PuertoInfo> puertosDisponibles = new();

        [ObservableProperty]
        private ObservableCollection<CamaraInfo> camarasDisponibles = new();

        [ObservableProperty]
        private PuertoInfo puertoSeleccionado;

        [ObservableProperty]
        private CamaraInfo camaraSeleccionada;

        [ObservableProperty]
        private string textoEstadoConexionHardware = "🔴 Desconectado";

        [ObservableProperty]
        private Brush fondoEstadoConexion = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FEE2E2"));

        [ObservableProperty]
        private Brush textoColorEstadoConexion = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DC2626"));

        [ObservableProperty]
        private string textoEstadoCamara = "Sistema Listo";

        [ObservableProperty]
        private Brush textoColorEstadoCamara = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#39A900"));

        [ObservableProperty]
        private bool btnConectarHardwareEnabled = true;

        [ObservableProperty]
        private bool btnDesconectarHardwareEnabled = false;

        [ObservableProperty]
        private bool btnGuardarEnabled = false;

        [ObservableProperty]
        private bool btnClasificacionManualEnabled = false;

        // ──────────────────────────────────────────────────────────────
        // Streaming de Cámara (BitmapImage congelado para thread-safety)
        // ──────────────────────────────────────────────────────────────
        [ObservableProperty]
        private ImageSource cameraFrame;

        // ──────────────────────────────────────────────────────────────
        // Historial Reciente y Paginación
        // ──────────────────────────────────────────────────────────────
        [ObservableProperty]
        private ObservableCollection<ClasificacionProduccion> historialProduccion = new();

        [ObservableProperty]
        private ObservableCollection<ClasificacionProduccion> historialPaginado = new();

        [ObservableProperty]
        private int paginaActual = 1;

        [ObservableProperty]
        private int totalPaginas = 1;

        [ObservableProperty]
        private int elementosPorPagina = 10;

        // ──────────────────────────────────────────────────────────────
        // Servicios de Base de Datos y Cliente HTTP
        // ──────────────────────────────────────────────────────────────
        private readonly ClasificacionProduccionDatabase dbProduccion = new();
        private readonly DetalleClasificacionDatabase dbDetalle = new();
        private readonly LoteDatabase dbLote = new();

        private static readonly HttpClient httpClient = new HttpClient();
        private bool leyendoBascula = false;

        private DateTime horaInicioLote = DateTime.Now;
        private DateTime ultimaDeteccion = DateTime.MinValue;

        // ──────────────────────────────────────────────────────────────
        // Comandos ICommand (RelayCommand)
        // ──────────────────────────────────────────────────────────────
        public ICommand GuardarClasificacionCommand { get; }
        public ICommand RefrescarLotesCommand { get; }
        public ICommand ConectarHardwareCommand { get; }
        public ICommand DesconectarHardwareCommand { get; }
        public ICommand RefrescarHardwareCommand { get; }
        public ICommand RegistrarHuevoManualCommand { get; }
        public ICommand PaginaAnteriorCommand { get; }
        public ICommand PaginaSiguienteCommand { get; }

        public produccionViewModel()
        {
            RefrescarUsuarioActual();
            horaInicioLote = DateTime.Now;

            GuardarClasificacionCommand = new RelayCommand(async _ => await GuardarClasificacionAutomaticaAsync(), _ => BtnGuardarEnabled && TotalResumen > 0);
            RefrescarLotesCommand = new RelayCommand(async _ => await CargarLotesAsync());
            ConectarHardwareCommand = new RelayCommand(async _ => await ConectarHardwareAsync(), _ => BtnConectarHardwareEnabled);
            DesconectarHardwareCommand = new RelayCommand(async _ => await DesconectarHardwareAsync(), _ => BtnDesconectarHardwareEnabled);
            RefrescarHardwareCommand = new RelayCommand(async _ => await CargarDispositivosDisponiblesAsync());
            RegistrarHuevoManualCommand = new RelayCommand(_ => RegistrarHuevoManual());
            PaginaAnteriorCommand = new RelayCommand(_ => CambiarPagina(-1), _ => PaginaActual > 1);
            PaginaSiguienteCommand = new RelayCommand(_ => CambiarPagina(1), _ => PaginaActual < TotalPaginas);

            _ = CargarLotesAsync();
            _ = CargarHistorialAsync();
            _ = ActualizarEstadisticasAsync();
            _ = CargarConfiguracionYDispositivosAsync();
        }

        public void CambiarPagina(int delta)
        {
            PaginaActual += delta;
            AplicarPaginacionHistorial();
        }



        // =====================================================================
        // INTEGRACIÓN HTTP CON LA API FLASK EN PYTHON (http://localhost:5001)
        // =====================================================================

        public void IniciarBuclesPolling()
        {
            leyendoBascula = true;

            Task.Run(async () =>
            {
                while (leyendoBascula)
                {
                    await ConsultarDatosHuevoAsync();
                    await Task.Delay(300);
                }
            });

            Task.Run(async () =>
            {
                while (leyendoBascula)
                {
                    await ConsultarFrameCamaraAsync();
                    await Task.Delay(80);
                }
            });
        }

        public void DetenerBuclesPolling()
        {
            leyendoBascula = false;
        }

        public async Task ConsultarDatosHuevoAsync()
        {
            try
            {
                HttpResponseMessage response = await httpClient.GetAsync("http://localhost:5001/datos-huevo");
                if (response.IsSuccessStatusCode)
                {
                    string jsonResponse = await response.Content.ReadAsStringAsync();
                    var opciones = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    var datos = JsonSerializer.Deserialize<loginavicola.Model.DatosHuevo>(jsonResponse, opciones);

                    if (datos != null)
                    {
                        Application.Current?.Dispatcher?.Invoke(() =>
                        {
                            PesoGramos = datos.Peso;
                            PesoRealTexto = $"{datos.Peso} g";
                            CategoriaActual = string.IsNullOrEmpty(datos.Categoria) ? "-" : datos.Categoria;
                            VolumenTexto = $"{datos.Volumen_Real:F1} cm³";
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                Application.Current?.Dispatcher?.Invoke(() =>
                {
                    TextoEstadoCamara = $"⚠️ Sin conexión a la API Python (datos): {ex.Message}";
                    TextoColorEstadoCamara = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DC2626"));
                });
            }
        }

        public async Task ConsultarFrameCamaraAsync()
        {
            try
            {
                byte[] frameBytes = await httpClient.GetByteArrayAsync("http://localhost:5001/frame.jpg");

                if (frameBytes == null || frameBytes.Length == 0) return;

                using (var ms = new MemoryStream(frameBytes))
                {
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.StreamSource = ms;
                    bitmap.EndInit();
                    bitmap.Freeze();

                    Application.Current?.Dispatcher?.Invoke(() =>
                    {
                        CameraFrame = bitmap;
                        TextoEstadoCamara = "Sistema Listo";
                        TextoColorEstadoCamara = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#39A900"));
                    });
                }
            }
            catch (Exception ex)
            {
                Application.Current?.Dispatcher?.Invoke(() =>
                {
                    TextoEstadoCamara = $"⚠️ Sin conexión a la API Python (video): {ex.Message}";
                    TextoColorEstadoCamara = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DC2626"));
                });
            }
        }

        public void ActualizarBadgeEstadoHardware(bool conectado, string mensaje)
        {
            Application.Current?.Dispatcher?.Invoke(() =>
            {
                TextoEstadoConexionHardware = mensaje;
                BtnDesconectarHardwareEnabled = conectado;

                if (mensaje.Contains("Buscando") || mensaje.Contains("Conectando") || mensaje.Contains("Desconectando"))
                {
                    FondoEstadoConexion = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FEF3C7"));
                    TextoColorEstadoConexion = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D97706"));
                }
                else if (conectado)
                {
                    FondoEstadoConexion = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DCFCE7"));
                    TextoColorEstadoConexion = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#16A34A"));
                }
                else
                {
                    FondoEstadoConexion = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FEE2E2"));
                    TextoColorEstadoConexion = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DC2626"));
                }
            });
        }

        public async Task CargarConfiguracionYDispositivosAsync()
        {
            ActualizarBadgeEstadoHardware(false, "⏳ Buscando...");
            Application.Current?.Dispatcher?.Invoke(() => BtnConectarHardwareEnabled = false);

            try
            {
                await CargarDispositivosDisponiblesAsync();

                string json = await httpClient.GetStringAsync("http://localhost:5001/estado-configuracion");
                var config = JsonSerializer.Deserialize<EstadoConfiguracionResponse>(json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                Application.Current?.Dispatcher?.Invoke(() =>
                {
                    if (config != null && config.Configurado)
                    {
                        if (!string.IsNullOrEmpty(config.PuertoBascula))
                        {
                            PuertoSeleccionado = PuertosDisponibles?.FirstOrDefault(p => p.Puerto == config.PuertoBascula);
                        }

                        CamaraSeleccionada = CamarasDisponibles?.FirstOrDefault(c => c.Id == config.CamaraIndex);

                        ActualizarBadgeEstadoHardware(config.Conectado, config.Conectado ? "🟢 Conectado" : "🔴 Desconectado");
                    }
                    else
                    {
                        ActualizarBadgeEstadoHardware(false, "🔴 Desconectado");
                        TextoEstadoCamara = "Hardware no configurado. Elige cámara y puerto arriba.";
                    }
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error al consultar estado de configuración: {ex.Message}");
                ActualizarBadgeEstadoHardware(false, "🔴 Desconectado");
            }
            finally
            {
                Application.Current?.Dispatcher?.Invoke(() => BtnConectarHardwareEnabled = true);
            }
        }

        public async Task CargarDispositivosDisponiblesAsync()
        {
            try
            {
                string json = await httpClient.GetStringAsync("http://localhost:5001/dispositivos-disponibles");
                var datos = JsonSerializer.Deserialize<DispositivosResponse>(json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                var puertos = datos?.Puertos ?? new List<PuertoInfo>();
                var camaras = datos?.Camaras ?? new List<CamaraInfo>();

                Application.Current.Dispatcher.Invoke(() =>
                {
                    PuertosDisponibles = new ObservableCollection<PuertoInfo>(puertos);
                    CamarasDisponibles = new ObservableCollection<CamaraInfo>(camaras);

                    if (PuertoSeleccionado == null && PuertosDisponibles.Count > 0)
                        PuertoSeleccionado = PuertosDisponibles[0];

                    if (CamaraSeleccionada == null && CamarasDisponibles.Count > 0)
                        CamaraSeleccionada = CamarasDisponibles[0];
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error al cargar dispositivos disponibles: {ex.Message}");
            }
        }

        public async Task ConectarHardwareAsync()
        {
            if (PuertoSeleccionado == null || string.IsNullOrEmpty(PuertoSeleccionado.Puerto))
            {
                MessageBox.Show("Por favor selecciona un puerto para la báscula.", "Falta Información", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (CamaraSeleccionada == null)
            {
                MessageBox.Show("Por favor selecciona una cámara.", "Falta Información", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            Application.Current?.Dispatcher?.Invoke(() => BtnConectarHardwareEnabled = false);
            ActualizarBadgeEstadoHardware(false, "⏳ Conectando...");

            try
            {
                var payload = JsonSerializer.Serialize(new
                {
                    puerto_bascula = PuertoSeleccionado.Puerto,
                    camara_index = CamaraSeleccionada.Id
                });

                var contenido = new StringContent(payload, System.Text.Encoding.UTF8, "application/json");
                var respuesta = await httpClient.PostAsync("http://localhost:5001/guardar-configuracion", contenido);

                if (respuesta.IsSuccessStatusCode)
                {
                    string jsonResult = await respuesta.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(jsonResult);
                    bool conectado = doc.RootElement.TryGetProperty("conectado", out var prop) && prop.GetBoolean();

                    ActualizarBadgeEstadoHardware(conectado, conectado ? "🟢 Conectado" : "🔴 Desconectado");
                    if (conectado)
                    {
                        IniciarBuclesPolling();
                    }
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
                Application.Current?.Dispatcher?.Invoke(() => BtnConectarHardwareEnabled = true);
            }
        }

        public async Task DesconectarHardwareAsync()
        {
            Application.Current?.Dispatcher?.Invoke(() =>
            {
                BtnConectarHardwareEnabled = false;
                BtnDesconectarHardwareEnabled = false;
            });
            ActualizarBadgeEstadoHardware(false, "⏳ Desconectando...");

            try
            {
                DetenerBuclesPolling();

                var respuesta = await httpClient.PostAsync("http://localhost:5001/desconectar-hardware", null);
                if (respuesta.IsSuccessStatusCode)
                {
                    ActualizarBadgeEstadoHardware(false, "🔴 Desconectado");
                    Application.Current?.Dispatcher?.Invoke(() =>
                    {
                        CameraFrame = null;
                        TextoEstadoCamara = "Hardware desconectado.";
                        TextoColorEstadoCamara = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DC2626"));
                        BtnConectarHardwareEnabled = true;
                        BtnDesconectarHardwareEnabled = false;
                    });
                }
                else
                {
                    ActualizarBadgeEstadoHardware(false, "🔴 Error al Desconectar");
                    Application.Current?.Dispatcher?.Invoke(() =>
                    {
                        BtnConectarHardwareEnabled = true;
                        BtnDesconectarHardwareEnabled = true;
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error al desconectar hardware: {ex.Message}");
                ActualizarBadgeEstadoHardware(false, "🔴 Desconectado");
                Application.Current?.Dispatcher?.Invoke(() =>
                {
                    BtnConectarHardwareEnabled = true;
                    BtnDesconectarHardwareEnabled = false;
                });
            }
        }

        // ──────────────────────────────────────────────────────────────
        // Servicios de Base de Datos y control interno
        // ──────────────────────────────────────────────────────────────


        public void RefrescarUsuarioActual()
        {
            var user = loginavicola.UserSession.UsuarioActual;
            bool esVisitante = loginavicola.UserSession.EsVisitante;

            if (esVisitante)
            {
                UsuarioActualNombre = "Visitante";
            }
            else if (user != null)
            {
                string nombreComp = user.NombreCompleto?.Trim();
                if (!string.IsNullOrEmpty(nombreComp))
                {
                    UsuarioActualNombre = nombreComp;
                }
                else if (!string.IsNullOrEmpty(user.Username))
                {
                    UsuarioActualNombre = user.Username;
                }
                else
                {
                    UsuarioActualNombre = "Invitado";
                }
            }
            else
            {
                UsuarioActualNombre = "Invitado";
            }
        }

        public void RecalcularTotalResumen()
        {
            TotalResumen = ContadorJumbo + ContadorAAA + ContadorAA + ContadorA + ContadorB + ContadorC;
        }

        // =====================================================================
        // ACCESO A BASE DE DATOS: SELECCIÓN Y CARGA DE LOTES
        // =====================================================================

        public async Task CargarLotesAsync()
        {
            try
            {
                var lotes = await Task.Run(() => dbLote.ObtenerTodosLosLotes());
                var itemsCombo = lotes.Select(l => new LoteComboItem
                {
                    IdLote = l.IdLote,
                    Display = $"Lote #{l.IdLote} - {l.Raza} ({l.FechaIncorporacion:dd/MM/yyyy})"
                }).ToList();

                Application.Current.Dispatcher.Invoke(() =>
                {
                    LotesDisponibles = new ObservableCollection<LoteComboItem>(itemsCombo);

                    if (itemsCombo.Count == 0)
                    {
                        TextoEstadoLote = "⚠️ No hay lotes registrados. Crea un lote primero.";
                    }
                    else
                    {
                        TextoEstadoLote = "⚠️ Selecciona un lote para comenzar a clasificar";
                    }
                    ColorEstadoLote = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DC2626"));

                    IdLoteSeleccionado = 0;
                    NombreLoteSeleccionado = "";
                    LoteSeleccionado = null;
                    BtnGuardarEnabled = false;
                    BtnClasificacionManualEnabled = false;
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error al cargar lotes: {ex.Message}");
            }
        }

        public void SeleccionarLoteItem(LoteComboItem item)
        {
            if (item != null)
            {
                LoteSeleccionado = item;
                IdLoteSeleccionado = item.IdLote;
                NombreLoteSeleccionado = item.Display;

                TextoEstadoLote = $"✅ {item.Display} seleccionado";
                ColorEstadoLote = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#16A34A"));

                BtnGuardarEnabled = true;
                BtnClasificacionManualEnabled = true;

                // Reiniciar contadores del lote anterior
                ContadorJumbo = 0;
                ContadorAAA = 0;
                ContadorAA = 0;
                ContadorA = 0;
                ContadorB = 0;
                ContadorC = 0;
                RecalcularTotalResumen();
                horaInicioLote = DateTime.Now;
            }
            else
            {
                IdLoteSeleccionado = 0;
                NombreLoteSeleccionado = "";
                LoteSeleccionado = null;
                BtnGuardarEnabled = false;
                BtnClasificacionManualEnabled = false;
            }
        }

        // =====================================================================
        // CLASIFICACIÓN DE HUEVOS Y CÁLCULOS
        // =====================================================================

        public string ClasificarHuevo(double peso)
        {
            if (peso >= 78) return "Jumbo";
            if (peso >= 67) return "AAA";
            if (peso >= 60) return "AA";
            if (peso >= 53) return "A";
            if (peso >= 46) return "B";
            return "C";
        }

        public void ContarHuevoEnMemoria(string categoria)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                switch (categoria)
                {
                    case "Jumbo": ContadorJumbo++; break;
                    case "AAA": ContadorAAA++; break;
                    case "AA": ContadorAA++; break;
                    case "A": ContadorA++; break;
                    case "B": ContadorB++; break;
                    case "C": ContadorC++; break;
                }
                RecalcularTotalResumen();
            });
        }

        public void RegistrarHuevoManual()
        {
            if (IdLoteSeleccionado <= 0)
            {
                TextoEstadoCamara = "⚠️ Selecciona un lote antes de clasificar.";
                TextoColorEstadoCamara = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DC2626"));
                return;
            }

            if (PesoGramos <= 0)
            {
                TextoEstadoCamara = "⚠️ Báscula en 0. Pon un huevo.";
                TextoColorEstadoCamara = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DC2626"));
                return;
            }

            if ((DateTime.Now - ultimaDeteccion).TotalSeconds < 1.2) return;

            ultimaDeteccion = DateTime.Now;
            string categoria = ClasificarHuevo(PesoGramos);
            ContarHuevoEnMemoria(categoria);
            TextoEstadoCamara = $"✅ Contabilizado: {PesoGramos}g - {categoria}";
            TextoColorEstadoCamara = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#39A900"));
        }

        // =====================================================================
        // ACCESO A BASE DE DATOS: GUARDADO Y HISTORIAL
        // =====================================================================

        public async Task<bool> GuardarClasificacionAutomaticaAsync()
        {
            if (IdLoteSeleccionado <= 0 || TotalResumen <= 0) return false;

            try
            {
                RefrescarUsuarioActual();
                string usuarioRecolector = string.IsNullOrEmpty(UsuarioActualNombre) ? "Sistema Visión" : UsuarioActualNombre;

                var clasificacion = new ClasificacionProduccion
                {
                    IdLote = IdLoteSeleccionado,
                    Fecha = DateTime.Now.Date,
                    HoraInicio = horaInicioLote.ToString("HH:mm:ss"),
                    HoraFin = DateTime.Now.ToString("HH:mm:ss"),
                    Recolector = usuarioRecolector,
                    TipoClasificacion = "Automática",
                    Jumbo = ContadorJumbo,
                    AAA = ContadorAAA,
                    AA = ContadorAA,
                    A = ContadorA,
                    B = ContadorB,
                    C = ContadorC,
                    Total = TotalResumen,
                    Observaciones = "Clasificación automática por báscula/cámara"
                };

                bool exito = await Task.Run(() => dbProduccion.InsertarClasificacion(clasificacion));

                if (exito)
                {
                    ContadorJumbo = 0;
                    ContadorAAA = 0;
                    ContadorAA = 0;
                    ContadorA = 0;
                    ContadorB = 0;
                    ContadorC = 0;
                    RecalcularTotalResumen();
                    horaInicioLote = DateTime.Now;

                    await CargarHistorialAsync();
                    await ActualizarEstadisticasAsync();
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error al guardar clasificación: {ex.Message}");
                return false;
            }
        }

        public async Task CargarHistorialAsync()
        {
            try
            {
                var datos = await Task.Run(() => dbProduccion.ObtenerHistorial(100));
                Application.Current.Dispatcher.Invoke(() =>
                {
                    HistorialProduccion = new ObservableCollection<ClasificacionProduccion>(datos);
                    AplicarPaginacionHistorial();
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error cargando historial: {ex.Message}");
            }
        }

        [ObservableProperty]
        private string textoBusqueda = string.Empty;

        partial void OnTextoBusquedaChanged(string value)
        {
            PaginaActual = 1;
            AplicarPaginacionHistorial();
        }

        public void AplicarPaginacionHistorial()
        {
            if (HistorialProduccion == null || HistorialProduccion.Count == 0)
            {
                TotalPaginas = 1;
                PaginaActual = 1;
                HistorialPaginado = new ObservableCollection<ClasificacionProduccion>();
                return;
            }

            IEnumerable<ClasificacionProduccion> fuente = HistorialProduccion;

            if (!string.IsNullOrWhiteSpace(TextoBusqueda))
            {
                string query = TextoBusqueda.Trim().ToLowerInvariant();
                fuente = fuente.Where(item =>
                    (item.Recolector != null && item.Recolector.ToLowerInvariant().Contains(query)) ||
                    (item.TipoClasificacion != null && item.TipoClasificacion.ToLowerInvariant().Contains(query)) ||
                    item.IdLote.ToString().Contains(query) ||
                    item.Fecha.ToString("dd/MM/yyyy").Contains(query)
                );
            }

            var listaFiltrada = fuente.ToList();

            if (listaFiltrada.Count == 0)
            {
                TotalPaginas = 1;
                PaginaActual = 1;
                HistorialPaginado = new ObservableCollection<ClasificacionProduccion>();
                return;
            }

            int tamano = ElementosPorPagina > 0 ? ElementosPorPagina : 10;
            TotalPaginas = (int)Math.Ceiling((double)listaFiltrada.Count / tamano);
            if (PaginaActual < 1) PaginaActual = 1;
            if (PaginaActual > TotalPaginas) PaginaActual = TotalPaginas;

            var items = listaFiltrada.Skip((PaginaActual - 1) * tamano).Take(tamano).ToList();
            HistorialPaginado = new ObservableCollection<ClasificacionProduccion>(items);
        }

        public async Task ActualizarEstadisticasAsync()
        {
            try
            {
                await Task.Run(() => dbProduccion.ObtenerProduccionHoy());
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error actualizando estadísticas: {ex.Message}");
            }
        }
    }
}
