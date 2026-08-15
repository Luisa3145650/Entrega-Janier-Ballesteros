using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace loginavicola.Model
{
    public class DetalleClasificacion
    {
        public int IdDetalle { get; set; }

        public int IdClasificacion { get; set; }

        public double Peso { get; set; }

        public double Volumen { get; set; }

        public string Categoria { get; set; } = "";

        public DateTime FechaHora { get; set; }

        public string Origen { get; set; } = "";
    }
}