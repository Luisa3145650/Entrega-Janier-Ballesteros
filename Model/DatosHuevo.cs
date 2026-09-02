using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace loginavicola.Model
{
    public class DatosHuevo
    {
        [JsonPropertyName("largo")]
        public double Largo { get; set; }

        [JsonPropertyName("ancho")]
        public double Ancho { get; set; }

        [JsonPropertyName("peso")]
        public double Peso { get; set; }

        [JsonPropertyName("elipsoide")]
        public double Elipsoide { get; set; }

        [JsonPropertyName("revolucion")]
        public double Revolucion { get; set; }

        [JsonPropertyName("bascula")]
        public object Bascula { get; set; }

        [JsonPropertyName("volumen_real")]
        public double Volumen_Real { get; set; }

        [JsonPropertyName("volumen")]
        public double Volumen
        {
            get => Volumen_Real;
            set => Volumen_Real = value;
        }

        [JsonPropertyName("categoria")]
        public string Categoria { get; set; }

        [JsonPropertyName("metodo_deteccion")]
        public string MetodoDeteccion { get; set; }

        [JsonPropertyName("huevo_detectado")]
        public bool HuevoDetectado { get; set; }

        [JsonPropertyName("es_valido")]
        public bool EsValido { get; set; }
    }
}
