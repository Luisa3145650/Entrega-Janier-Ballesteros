using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace loginavicola.Model
{
    public class Vacunacion
    {
        public string IdVacunacion { get; set; } = Guid.NewGuid().ToString().Substring(0, 8);
        public string IdLote { get; set; } = string.Empty;
        public DateTime FechaVacunacion { get; set; } = DateTime.Now;
        public string Vacuna { get; set; } = string.Empty;
        public string Dosis { get; set; } = string.Empty;
        public string Responsable { get; set; } = string.Empty;
        public string Observaciones { get; set; } = string.Empty;
    }
}

