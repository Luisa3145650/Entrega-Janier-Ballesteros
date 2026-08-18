using System;

namespace loginavicola.Model
{
    public enum TipoAlerta
    {
        StockBajo,
        LoteDescarte
    }

    public enum SeveridadAlerta
    {
        Advertencia,
        Critico
    }

    public class AlertaSistema
    {
        public TipoAlerta Tipo { get; set; }
        public SeveridadAlerta Severidad { get; set; }
        public string Titulo { get; set; } = string.Empty;
        public string Mensaje { get; set; } = string.Empty;
        public string DetalleExtra { get; set; } = string.Empty;
        public DateTime FechaDeteccion { get; set; } = DateTime.Now;

        public string IconoSeveridad => Severidad == SeveridadAlerta.Critico ? "🚨" : "⚠️";
        public string ColorSeveridad => Severidad == SeveridadAlerta.Critico ? "#DC2626" : "#D97706";
        public string FondoSeveridad => Severidad == SeveridadAlerta.Critico ? "#FEE2E2" : "#FEF3C7";
    }
}
