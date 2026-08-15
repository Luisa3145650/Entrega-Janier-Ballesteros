using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
namespace loginavicola.Model
{
    public class Consumo
    {
        public int IdConsumo { get; set; }
        public DateTime FechaConsumo { get; set; }
        public int IdLoteGallinas { get; set; }
        public int IdAlimento { get; set; }
        public decimal CantidadConsumida { get; set; }
        public string UnidadMedida { get; set; } = string.Empty;
        public string Turno { get; set; } = string.Empty;
        public string Observaciones { get; set; } = string.Empty;

        // Propiedades adicionales para control semanal
        public int NumeroSemana { get; set; }
        public int Año { get; set; }
        public int CantidadGallinas { get; set; }
        public decimal ConsumoEsperado { get; set; }
        public decimal Merma { get; set; }
        public bool AlertaMerma { get; set; }

        // Propiedades calculadas para el DataGrid
        public string NombreAlimento { get; set; } = string.Empty;
        public string RacionDiaria => "60g AM / 60g PM";
        public string ConsumoSemanal => $"{CantidadConsumida}kg";
    }

    public class Alimento
    {
        public int IdAlimento { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string UnidadMedida { get; set; } = "kg";

        // ✅ AGREGAR ESTA PROPIEDAD si no la tienes
        public int StockDisponible { get; set; }
    }

    public class LoteGallina
    {
        public int IdLote { get; set; }
        public string Raza { get; set; } = string.Empty;
        public int CantidadActual { get; set; }
        public DateTime FechaIngreso { get; set; }

        // Propiedad calculada para mostrar en ComboBox
        public string DisplayText => $"Lote {IdLote} - {Raza} ({CantidadActual} gallinas)";
    }
}