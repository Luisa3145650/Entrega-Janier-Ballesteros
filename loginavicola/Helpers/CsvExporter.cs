using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
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
                SaveFileDialog saveDialog = new SaveFileDialog
                {
                    FileName = $"{nombreArchivo}_{DateTime.Now:yyyyMMdd_HHmm}.csv",
                    Filter = "Archivos CSV (*.csv)|*.csv",
                    DefaultExt = "csv"
                };

                if (saveDialog.ShowDialog() == true)
                {
                    using (StreamWriter writer = new StreamWriter(saveDialog.FileName, false, Encoding.UTF8))
                    {
                        writer.WriteLine(string.Join(";", encabezados));

                        foreach (var item in datos)
                        {
                            string[] valores = obtenerValores(item);
                            for (int i = 0; i < valores.Length; i++)
                            {
                                string val = valores[i]?.Replace("\r", "").Replace("\n", " ") ?? "";
                                if (val.Contains(";") || val.Contains("\""))
                                    val = $"\"{val.Replace("\"", "\"\"")}\"";
                                valores[i] = val;
                            }
                            writer.WriteLine(string.Join(";", valores));
                        }
                    }
                    MessageBox.Show("Reporte generado con éxito.", "AvícolaSena", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error crítico: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}