using ClosedXML.Excel;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace loginavicola.Helpers
{
    public static class ExcelHelper
    {
        public static void ExportarAExcel<T>(List<T> datos, string nombreHoja = "Datos")
        {
            var saveFileDialog = new SaveFileDialog
            {
                Filter = "Archivo Excel (*.xlsx)|*.xlsx",
                FileName = $"Reporte_Avicola_{DateTime.Now:yyyyMMdd}.xlsx"
            };

            if (saveFileDialog.ShowDialog() != true)
                return;

            using (var workbook = new XLWorkbook())
            {
                var worksheet = workbook.Worksheets.Add(nombreHoja ?? "Datos");
                var propiedades = typeof(T).GetProperties();

                // 1. Escribir Encabezados
                for (int i = 0; i < propiedades.Length; i++)
                {
                    var celda = worksheet.Cell(1, i + 1);
                    celda.Value = propiedades[i].Name;
                }

                // 2. Escribir Datos
                for (int fila = 0; fila < datos.Count; fila++)
                {
                    for (int col = 0; col < propiedades.Length; col++)
                    {
                        var valor = propiedades[col].GetValue(datos[fila]);
                        worksheet.Cell(fila + 2, col + 1).Value = valor?.ToString() ?? "";
                    }
                }

                // 3. FORMATO DE TABLA TÉCNICA (Estándar Profesional)
                if (datos.Count > 0)
                {
                    var rango = worksheet.Range(1, 1, datos.Count + 1, propiedades.Length);
                    var tabla = rango.CreateTable();

                    // Usamos un estilo "Light" que es blanco y gris, muy limpio
                    tabla.Theme = XLTableTheme.TableStyleLight1;

                    // Forzar bordes negros delgados para que parezca una tabla de verdad
                    rango.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                    rango.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
                    rango.Style.Border.OutsideBorderColor = XLColor.Black;
                    rango.Style.Border.InsideBorderColor = XLColor.Black;

                    // Formato específico para el encabezado
                    var encabezado = worksheet.Range(1, 1, 1, propiedades.Length);
                    encabezado.Style.Fill.BackgroundColor = XLColor.FromHtml("#D3D3D3"); // Gris institucional
                    encabezado.Style.Font.Bold = true;
                    encabezado.Style.Font.FontColor = XLColor.Black;
                    encabezado.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                }

                // 4. AJUSTES FINALES
                worksheet.Columns().AdjustToContents(); // Ajusta el ancho automáticamente

                workbook.SaveAs(saveFileDialog.FileName);
            }
        }
    }
}