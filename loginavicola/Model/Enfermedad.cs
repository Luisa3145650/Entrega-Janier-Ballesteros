using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace loginavicola.Model
{
    public class Enfermedad
    {
        public string Nombre { get; set; } = string.Empty;
        public string Sintomas { get; set; } = string.Empty;
        public string TratamientoRecomendado { get; set; } = string.Empty;
        public string SeveridadTypica { get; set; } = string.Empty;
    }
}

