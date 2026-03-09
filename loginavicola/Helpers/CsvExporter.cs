using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Windows;
using Microsoft.Win32;

namespace loginavicola.Helpers
{
    public static class CsvExporter
    {
        public static void ExportarACSV<T>(List<T> datos, string nombreArchivo, string[] encabezados, Func<T, string[]> obtenerValores)
        {
            try
            {
                // Mostrar diálogo para guardar archivo
                SaveFileDialog saveDialog = new SaveFileDialog
                {
                    FileName = $"{nombreArchivo}_{DateTime.Now:yyyyMMdd_HHmmss}.csv",
                    Filter = "Archivos CSV (*.csv)|*.csv|Todos los archivos (*.*)|*.*",
                    DefaultExt = "csv"
                };

                if (saveDialog.ShowDialog() == true)
                {
                    using (StreamWriter writer = new StreamWriter(saveDialog.FileName, false, Encoding.UTF8))
                    {
                        // Escribir encabezados
                        writer.WriteLine(string.Join(",", encabezados));

                        // Escribir datos
                        foreach (var item in datos)
                        {
                            string[] valores = obtenerValores(item);

                            // Escapar comillas y comas en los valores
                            for (int i = 0; i < valores.Length; i++)
                            {
                                if (valores[i].Contains(",") || valores[i].Contains("\""))
                                {
                                    valores[i] = $"\"{valores[i].Replace("\"", "\"\"")}\"";
                                }
                            }

                            writer.WriteLine(string.Join(",", valores));
                        }
                    }

                    MessageBox.Show($"Archivo exportado exitosamente:\n{saveDialog.FileName}",
                        "Exportación Exitosa",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al exportar: {ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
    }
}