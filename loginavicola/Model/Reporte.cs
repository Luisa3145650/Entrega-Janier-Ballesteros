using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace loginavicola.Model
{
    public class ClasificacionReporte
    {
        public int IdClasificacion { get; set; }
        public DateTime Fecha { get; set; }
        public string Recolector { get; set; } = string.Empty;
        public int Jumbo { get; set; }
        public int AAA { get; set; }
        public int AA { get; set; }
        public int A { get; set; }
        public int B { get; set; }
        public int C { get; set; }
        public int Total => Jumbo + AAA + AA + A + B + C;
    }

    public class ProduccionReporte
    {
        public int IdProduccion { get; set; }
        public DateTime Fecha { get; set; }
        public int IdLote { get; set; }
        public string Raza { get; set; } = string.Empty;
        public int CantidadHuevos { get; set; }
        public decimal PorcentajeProduccion { get; set; }
        public string Observaciones { get; set; } = string.Empty;
    }
}
