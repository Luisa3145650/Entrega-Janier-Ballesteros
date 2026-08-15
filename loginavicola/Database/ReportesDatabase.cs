#nullable enable
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Windows;
using loginavicola.Model;

namespace loginavicola.Database
{
    public class ReportesDatabase
    {
        private readonly string connectionString;

        public ReportesDatabase()
        {
            string dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "sistema_avicola.db");
            connectionString = $"Data Source={dbPath};Version=3;";
        }

        // 1. INVENTARIO (Tabla real: Alimento)
        public List<ItemInventario> ObtenerInventario()
        {
            var lista = new List<ItemInventario>();
            try
            {
                using (var conn = new SQLiteConnection(connectionString))
                {
                    conn.Open();
                    string sql = "SELECT * FROM Alimento ORDER BY Nombre ASC";
                    using (var cmd = new SQLiteCommand(sql, conn))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            lista.Add(new ItemInventario
                            {
                                IdItem = reader["IdAlimento"] != DBNull.Value ? Convert.ToInt32(reader["IdAlimento"]) : 0,
                                Nombre = reader["Nombre"]?.ToString() ?? "",
                                Categoria = "Alimento",
                                CantidadStock = reader["StockDisponible"] != DBNull.Value ? Convert.ToInt32(reader["StockDisponible"]) : 0
                            });
                        }
                    }
                }
            }
            catch (Exception ex) { MessageBox.Show("Error Inventario: " + ex.Message); }
            return lista;
        }

        // 2. PRODUCCIÓN Y CLASIFICACIÓN (Tabla real: ClasificacionProduccion)
        // He creado este método con los dos nombres para que no te de error en reportesView
        public List<ClasificacionReporte> ObtenerProduccion() => ObtenerClasificaciones();

        public List<ClasificacionReporte> ObtenerClasificaciones()
        {
            var lista = new List<ClasificacionReporte>();
            try
            {
                using (var conn = new SQLiteConnection(connectionString))
                {
                    conn.Open();
                    string sql = "SELECT * FROM ClasificacionProduccion ORDER BY Fecha DESC";
                    using (var cmd = new SQLiteCommand(sql, conn))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            lista.Add(new ClasificacionReporte
                            {
                                IdClasificacion = Convert.ToInt32(reader["IdClasificacion"]),
                                Fecha = Convert.ToDateTime(reader["Fecha"]),
                                Recolector = reader["Recolector"]?.ToString() ?? "",
                                Jumbo = Convert.ToInt32(reader["Jumbo"]),
                                AAA = Convert.ToInt32(reader["AAA"]),
                                AA = Convert.ToInt32(reader["AA"]),
                                A = Convert.ToInt32(reader["A"]),
                                B = Convert.ToInt32(reader["B"]),
                                C = Convert.ToInt32(reader["C"])
                            });
                        }
                    }
                }
            }
            catch (Exception ex) { MessageBox.Show("Error Datos Producción: " + ex.Message); }
            return lista;
        }

        // 3. DIAGNÓSTICOS
        public List<Diagnostico> ObtenerDiagnosticos()
        {
            var lista = new List<Diagnostico>();
            try
            {
                using (var conn = new SQLiteConnection(connectionString))
                {
                    conn.Open();
                    string sql = "SELECT * FROM Diagnostico ORDER BY FechaDiagnostico DESC";
                    using (var cmd = new SQLiteCommand(sql, conn))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            lista.Add(new Diagnostico
                            {
                                IdDiagnostico = Convert.ToInt32(reader["IdDiagnostico"]),
                                FechaDiagnostico = Convert.ToDateTime(reader["FechaDiagnostico"]),
                                DiagnosticoMedico = reader["DiagnosticoMedico"]?.ToString() ?? "",
                                Tratamiento = reader["Tratamiento"]?.ToString() ?? "",
                                GallinasAfectadas = Convert.ToInt32(reader["GallinasAfectadas"]),
                                Veterinario = reader["Veterinario"]?.ToString() ?? ""
                            });
                        }
                    }
                }
            }
            catch (Exception ex) { MessageBox.Show("Error Diagnósticos: " + ex.Message); }
            return lista;
        }

        // 4. LOTES
        public List<Lote> ObtenerLotes()
        {
            var lista = new List<Lote>();
            try
            {
                using (var conn = new SQLiteConnection(connectionString))
                {
                    conn.Open();
                    string sql = "SELECT * FROM Lote ORDER BY IdLote DESC";
                    using (var cmd = new SQLiteCommand(sql, conn))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            lista.Add(new Lote
                            {
                                IdLote = Convert.ToInt32(reader["IdLote"]),
                                Raza = reader["Raza"]?.ToString() ?? "",
                                CantidadGallinas = Convert.ToInt32(reader["CantidadGallinas"]),
                                FechaIncorporacion = Convert.ToDateTime(reader["FechaIncorporacion"]),
                                Estado = reader["Estado"]?.ToString() ?? ""
                            });
                        }
                    }
                }
            }
            catch (Exception ex) { MessageBox.Show("Error Lotes: " + ex.Message); }
            return lista;
        }
    }
}