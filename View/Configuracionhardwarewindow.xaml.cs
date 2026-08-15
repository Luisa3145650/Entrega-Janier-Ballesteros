using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace loginavicola.View
{
    public partial class ConfiguracionHardwareWindow : Window
    {
        private static readonly HttpClient client = new HttpClient();
        private const string BASE_URL = "http://localhost:5001";

        public class PuertoInfo
        {
            [JsonPropertyName("puerto")]
            public string Puerto { get; set; }

            [JsonPropertyName("descripcion")]
            public string Descripcion { get; set; }
        }

        public class DispositivosResponse
        {
            [JsonPropertyName("puertos")]
            public List<PuertoInfo> Puertos { get; set; }

            [JsonPropertyName("camaras")]
            public List<string> Camaras { get; set; }
        }

        public class ConfiguracionResponse
        {
            [JsonPropertyName("puerto_bascula")]
            public string PuertoBascula { get; set; }

            [JsonPropertyName("camara_nombre")]
            public string CamaraNombre { get; set; }

            [JsonPropertyName("configurado")]
            public bool Configurado { get; set; }
        }

        public ConfiguracionHardwareWindow()
        {
            InitializeComponent();
            btnDetectar.Click += async (s, e) => await DetectarDispositivosAsync();
            btnGuardar.Click += async (s, e) => await GuardarYCerrarAsync();
            btnCancelar.Click += (s, e) => { DialogResult = false; Close(); };

            Loaded += async (s, e) => await CargarConfiguracionActualYDetectarAsync();
        }

        private async Task CargarConfiguracionActualYDetectarAsync()
        {
            await DetectarDispositivosAsync();

            // Si ya habia una configuracion previa (el usuario abrio esta ventana para
            // recalibrar, no porque sea la primera vez), preseleccionamos lo que ya tenia.
            try
            {
                string json = await client.GetStringAsync($"{BASE_URL}/estado-configuracion");
                var config = JsonSerializer.Deserialize<ConfiguracionResponse>(json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (config != null)
                {
                    if (!string.IsNullOrEmpty(config.PuertoBascula))
                        cmbPuertos.SelectedValue = config.PuertoBascula;

                    if (!string.IsNullOrEmpty(config.CamaraNombre))
                        cmbCamaras.SelectedItem = config.CamaraNombre;
                }
            }
            catch
            {
                // si falla, simplemente no preseleccionamos nada; no es critico
            }
        }

        private async Task DetectarDispositivosAsync()
        {
            txtEstado.Text = "Detectando dispositivos conectados...";
            txtEstado.Foreground = new SolidColorBrush(Colors.SlateGray);
            btnGuardar.IsEnabled = false;

            try
            {
                string json = await client.GetStringAsync($"{BASE_URL}/dispositivos-disponibles");
                var datos = JsonSerializer.Deserialize<DispositivosResponse>(json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                cmbPuertos.ItemsSource = datos?.Puertos ?? new List<PuertoInfo>();
                cmbCamaras.ItemsSource = datos?.Camaras ?? new List<string>();

                if (cmbPuertos.Items.Count > 0) cmbPuertos.SelectedIndex = 0;
                if (cmbCamaras.Items.Count > 0) cmbCamaras.SelectedIndex = 0;

                bool sinPuertos = datos?.Puertos == null || datos.Puertos.Count == 0;
                bool sinCamaras = datos?.Camaras == null || datos.Camaras.Count == 0;

                if (sinPuertos || sinCamaras)
                {
                    txtEstado.Text = "⚠️ " +
                        (sinPuertos ? "No se detectó ningún puerto serie. " : "") +
                        (sinCamaras ? "No se detectó ninguna cámara. " : "") +
                        "Revisa las conexiones y vuelve a detectar.";
                    txtEstado.Foreground = new SolidColorBrush(Colors.Crimson);
                }
                else
                {
                    txtEstado.Text = $"✅ {datos.Puertos.Count} puerto(s) y {datos.Camaras.Count} cámara(s) detectados.";
                    txtEstado.Foreground = new SolidColorBrush(Colors.ForestGreen);
                }

                btnGuardar.IsEnabled = true;
            }
            catch (Exception ex)
            {
                txtEstado.Text = $"⚠️ No se pudo conectar con el servicio Python: {ex.Message}";
                txtEstado.Foreground = new SolidColorBrush(Colors.Crimson);
            }
        }

        private async Task GuardarYCerrarAsync()
        {
            if (cmbPuertos.SelectedItem is not PuertoInfo puerto)
            {
                MessageBox.Show("Selecciona un puerto para la báscula.", "Falta información", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (cmbCamaras.SelectedItem is not string camara)
            {
                MessageBox.Show("Selecciona una cámara.", "Falta información", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            btnGuardar.IsEnabled = false;
            txtEstado.Text = "Guardando y reconectando dispositivos...";
            txtEstado.Foreground = new SolidColorBrush(Colors.SlateGray);

            try
            {
                var payload = JsonSerializer.Serialize(new
                {
                    puerto_bascula = puerto.Puerto,
                    camara_nombre = camara
                });

                var contenido = new StringContent(payload, Encoding.UTF8, "application/json");
                var respuesta = await client.PostAsync($"{BASE_URL}/guardar-configuracion", contenido);

                if (respuesta.IsSuccessStatusCode)
                {
                    DialogResult = true;
                    Close();
                }
                else
                {
                    txtEstado.Text = "⚠️ El servicio Python no pudo guardar la configuración.";
                    txtEstado.Foreground = new SolidColorBrush(Colors.Crimson);
                    btnGuardar.IsEnabled = true;
                }
            }
            catch (Exception ex)
            {
                txtEstado.Text = $"⚠️ Error guardando configuración: {ex.Message}";
                txtEstado.Foreground = new SolidColorBrush(Colors.Crimson);
                btnGuardar.IsEnabled = true;
            }
        }
    }
}
