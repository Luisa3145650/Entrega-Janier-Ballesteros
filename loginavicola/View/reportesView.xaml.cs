using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System;
using System.IO;
using Microsoft.Win32;
using loginavicola.Database;
using loginavicola.Helpers;

namespace loginavicola.View
{
    public partial class reportesView : UserControl
    {
        private readonly ReportesDatabase database;

        public reportesView()
        {
            InitializeComponent();
            database = new ReportesDatabase();
        }

        private void BtnExportarClasificacion_Click(object sender, RoutedEventArgs e)
        {
            var datos = database.ObtenerClasificaciones();

            if (datos.Count == 0)
            {
                MessageBox.Show("No hay datos de clasificación para exportar",
                    "Sin Datos", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            string[] encabezados = {
                "ID", "Fecha", "Recolector", "Jumbo", "AAA", "AA", "A", "B", "C", "Total"
            };

            CsvExporter.ExportarACSV(
                datos,
                "Clasificacion_Huevos",
                encabezados,
                item => new string[] {
                    item.IdClasificacion.ToString(),
                    item.Fecha.ToString("dd/MM/yyyy"),
                    item.Recolector,
                    item.Jumbo.ToString(),
                    item.AAA.ToString(),
                    item.AA.ToString(),
                    item.A.ToString(),
                    item.B.ToString(),
                    item.C.ToString(),
                    item.Total.ToString()
                }
            );
        }

        private void BtnExportarInventario_Click(object sender, RoutedEventArgs e)
        {
            var datos = database.ObtenerAlimentos();

            if (datos.Count == 0)
            {
                MessageBox.Show("No hay datos de inventario para exportar",
                    "Sin Datos", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            string[] encabezados = { "ID", "Nombre", "Stock Disponible (kg)" };

            CsvExporter.ExportarACSV(
                datos,
                "Inventario_Alimentos",
                encabezados,
                item => new string[] {
                    item.IdAlimento.ToString(),
                    item.Nombre,
                    item.StockDisponible.ToString("F2")
                }
            );
        }

        private void BtnExportarInsumos_Click(object sender, RoutedEventArgs e)
        {
            var datos = database.ObtenerConsumos();

            if (datos.Count == 0)
            {
                MessageBox.Show("No hay datos de consumo para exportar",
                    "Sin Datos", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            string[] encabezados = {
                "ID", "Fecha", "Lote", "Alimento", "Cantidad", "Unidad", "Turno", "Observaciones"
            };

            CsvExporter.ExportarACSV(
                datos,
                "Consumo_Alimentos",
                encabezados,
                item => new string[] {
                    item.IdConsumo.ToString(),
                    item.FechaConsumo.ToString("dd/MM/yyyy"),
                    item.IdLoteGallinas.ToString(),
                    item.NombreAlimento,
                    item.CantidadConsumida.ToString("F2"),
                    item.UnidadMedida,
                    item.Turno,
                    item.Observaciones
                }
            );
        }

        private void BtnExportarLotes_Click(object sender, RoutedEventArgs e)
        {
            var datos = database.ObtenerLotes();

            if (datos.Count == 0)
            {
                MessageBox.Show("No hay datos de lotes para exportar",
                    "Sin Datos", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            string[] encabezados = {
                "ID", "Raza", "Cantidad", "Fecha Incorporación", "Granja Origen", "Estado", "Observaciones"
            };

            CsvExporter.ExportarACSV(
                datos,
                "Lotes_Gallinas",
                encabezados,
                item => new string[] {
                    item.IdLote.ToString(),
                    item.Raza,
                    item.CantidadGallinas.ToString(),
                    item.FechaIncorporacion.ToString("dd/MM/yyyy"),
                    item.GranjaOrigen,
                    item.Estado,
                    item.Observaciones
                }
            );
        }

        private void BtnExportarProduccion_Click(object sender, RoutedEventArgs e)
        {
            var datos = database.ObtenerProduccion();

            if (datos.Count == 0)
            {
                MessageBox.Show("No hay datos de producción para exportar",
                    "Sin Datos", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            string[] encabezados = {
                "ID", "Fecha", "Lote", "Raza", "Cantidad Huevos", "% Producción", "Observaciones"
            };

            CsvExporter.ExportarACSV(
                datos,
                "Produccion_Diaria",
                encabezados,
                item => new string[] {
                    item.IdProduccion.ToString(),
                    item.Fecha.ToString("dd/MM/yyyy"),
                    item.IdLote.ToString(),
                    item.Raza,
                    item.CantidadHuevos.ToString(),
                    item.PorcentajeProduccion.ToString("F2") + "%",
                    item.Observaciones
                }
            );
        }

        private void BtnExportarTodo_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "¿Desea exportar TODOS los datos del sistema?\n\n" +
                "Se generarán múltiples archivos CSV.",
                "Confirmar Exportación Completa",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                BtnExportarClasificacion_Click(sender, e);
                BtnExportarLotes_Click(sender, e);
                BtnExportarInventario_Click(sender, e);
                BtnExportarInsumos_Click(sender, e);
                BtnExportarProduccion_Click(sender, e);

                MessageBox.Show("Exportación completa finalizada",
                    "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
    }
}
