using System;
using System.Collections.Generic;
using System.Linq;
using loginavicola.Database;
using loginavicola.Model;

namespace loginavicola.Helpers
{
    public static class AlertasService
    {
        // TODO: Pendiente confirmación final de negocio por parte del usuario (valor por defecto: 68 semanas)
        public const int SEMANAS_EDAD_DESCARTE = 68;

        public static List<AlertaSistema> ObtenerAlertasActivas()
        {
            var alertas = new List<AlertaSistema>();

            try
            {
                // 1. Alertas de Insumos / Inventario por debajo de Stock Mínimo
                var inventarioDb = new InventarioDatabase();
                var items = inventarioDb.ObtenerTodosItems();
                if (items != null)
                {
                    foreach (var item in items.Where(i => i.CantidadStock <= i.StockMinimo))
                    {
                        alertas.Add(new AlertaSistema
                        {
                            Tipo = TipoAlerta.StockBajo,
                            Severidad = SeveridadAlerta.Critico,
                            Titulo = $"Stock Crítico: {item.Nombre}",
                            Mensaje = $"El insumo '{item.Nombre}' ({item.Categoria}) está en o por debajo de su stock mínimo.",
                            DetalleExtra = $"Stock actual: {item.CantidadStock} unidades (Mínimo: {item.StockMinimo})"
                        });
                    }
                }

                // 2. Alertas de Lotes en o por encima de Edad de Descarte (>= 68 semanas)
                var loteDb = new LoteDatabase();
                var lotes = loteDb.ObtenerTodosLosLotes();
                if (lotes != null)
                {
                    foreach (var lote in lotes.Where(l => l.SemanasEdad >= SEMANAS_EDAD_DESCARTE))
                    {
                        alertas.Add(new AlertaSistema
                        {
                            Tipo = TipoAlerta.LoteDescarte,
                            Severidad = SeveridadAlerta.Advertencia,
                            Titulo = $"Lote en Edad de Descarte: Lote #{lote.IdLote}",
                            Mensaje = $"El Lote #{lote.IdLote} ({lote.Raza}) ha alcanzado la edad recomendada de descarte.",
                            DetalleExtra = $"Edad actual: {lote.SemanasEdad} semanas (Umbral descarte: {SEMANAS_EDAD_DESCARTE} semanas)"
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error obteniendo alertas del sistema: {ex.Message}");
            }

            return alertas;
        }
    }
}
