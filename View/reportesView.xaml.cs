#nullable enable
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using loginavicola.Database;
using loginavicola.Helpers;
using loginavicola.Model;

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

        // 1. EXPORTAR USUARIOS (Usa la tabla Usuario)
        private void BtnExportarUsuarios_Click(object sender, RoutedEventArgs e)
        {
            var datos = database.ObtenerUsuarios();

            if (datos.Count == 0)
            {
                MessageBox.Show("No hay registros de usuarios para exportar.", "Sin Datos", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            string[] encabezados = { "ID", "Documento", "Nombres", "Apellidos", "Usuario", "Email", "Teléfono", "Dirección", "Rol", "Estado", "Fecha Registro", "Permisos Asignados" };

            CsvExporter.ExportarACSV(
                datos,
                "Reporte_Usuarios_Sistema",
                encabezados,
                item => {
                    var permisos = new List<string>();
                    if (item.PermisoInicio) permisos.Add("Inicio");
                    if (item.PermisoLotes) permisos.Add("Lotes");
                    if (item.PermisoProduccion) permisos.Add("Producción");
                    if (item.PermisoAlimentacion) permisos.Add("Alimentación");
                    if (item.PermisoExportarDatos) permisos.Add("Exportar Datos");
                    if (item.PermisoDiagnostico) permisos.Add("Diagnóstico");
                    if (item.PermisoInventario) permisos.Add("Inventario");
                    if (item.PermisoGestionUsuarios) permisos.Add("Gestión Usuarios");

                    string resumenPermisos = permisos.Count > 0 ? string.Join(" | ", permisos) : "Ninguno";

                    return new string[] {
                        item.IdUsuario.ToString(),
                        item.Documento,
                        item.Nombres,
                        item.Apellidos,
                        item.Username,
                        item.Email,
                        item.Telefono,
                        item.Direccion,
                        item.Rol,
                        item.Activo ? "Activo" : "Inactivo",
                        item.FechaCreacion.ToString("dd/MM/yyyy"),
                        resumenPermisos
                    };
                }
            );
        }

        // 2. EXPORTAR INVENTARIO (Usa la tabla Inventario)
        private void BtnExportarInventario_Click(object sender, RoutedEventArgs e)
        {
            var datos = database.ObtenerInventario();
            if (datos.Count == 0)
            {
                MessageBox.Show("No hay datos en el inventario para exportar.", "Sin Datos", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            string[] encabezados = { "ID", "Nombre", "Categoría", "Costo Unitario", "Ubicación", "Stock Actual", "Stock Mínimo", "Stock Máximo", "Fecha Caducidad", "Observaciones" };

            CsvExporter.ExportarACSV(datos, "Inventario_Productos", encabezados,
                item => new string[] {
                    item.IdItem.ToString(),
                    item.Nombre,
                    item.Categoria,
                    item.CostoUnitario.ToString("F2"),
                    item.Ubicacion,
                    item.CantidadStock.ToString(),
                    item.StockMinimo.ToString(),
                    item.StockMaximo.ToString(),
                    item.FechaCaducidad.HasValue ? item.FechaCaducidad.Value.ToString("dd/MM/yyyy") : "N/A",
                    item.Observaciones
                });
        }

        // 3. EXPORTAR DIAGNÓSTICOS (Salud de las aves)
        private void BtnExportarInsumos_Click(object sender, RoutedEventArgs e)
        {
            var datos = database.ObtenerDiagnosticos();
            if (datos.Count == 0)
            {
                MessageBox.Show("No hay registros de diagnósticos médicos.", "Sin Datos");
                return;
            }

            string[] encabezados = { "Fecha", "Diagnóstico", "Tratamiento", "Afectadas", "Veterinario" };

            CsvExporter.ExportarACSV(datos, "Reporte_Sanitario", encabezados,
                item => new string[] {
                    item.FechaDiagnostico.ToString("dd/MM/yyyy"),
                    item.DiagnosticoMedico,
                    item.Tratamiento,
                    item.GallinasAfectadas.ToString(),
                    item.Veterinario
                });
        }

        // 4. EXPORTAR LOTES
        private void BtnExportarLotes_Click(object sender, RoutedEventArgs e)
        {
            var datos = database.ObtenerLotes();
            if (datos.Count == 0) return;

            string[] encabezados = { "ID", "Raza", "Cantidad", "Fecha Inc.", "Estado" };

            CsvExporter.ExportarACSV(datos, "Lotes_Gallinas", encabezados,
                item => new string[] {
                    item.IdLote.ToString(),
                    item.Raza,
                    item.CantidadGallinas.ToString(),
                    item.FechaIncorporacion.ToString("dd/MM/yyyy"),
                    item.Estado
                });
        }

        // 5. EXPORTAR PRODUCCIÓN (Usa la tabla ClasificacionProduccion)
        private void BtnExportarProduccion_Click(object sender, RoutedEventArgs e)
        {
            var datos = database.ObtenerProduccion();

            if (datos.Count == 0)
            {
                MessageBox.Show("No hay datos de producción para exportar.", "Sin Datos", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            string[] encabezados = { "ID", "Fecha", "Recolector", "Jumbo", "AAA", "AA", "A", "B", "C", "Total" };

            CsvExporter.ExportarACSV(
                datos,
                "Reporte_Produccion_Huevos",
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

        // Compatibilidad previa
        private void BtnExportarClasificacion_Click(object sender, RoutedEventArgs e)
        {
            BtnExportarProduccion_Click(sender, e);
        }

        // 6. EXPORTAR TODO
        private void BtnExportarTodo_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show("¿Desea exportar todos los reportes disponibles?", "Confirmar", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes)
            {
                BtnExportarUsuarios_Click(sender, e);
                BtnExportarProduccion_Click(sender, e);
                BtnExportarLotes_Click(sender, e);
                BtnExportarInventario_Click(sender, e);
                BtnExportarInsumos_Click(sender, e);
            }
        }
    }
}