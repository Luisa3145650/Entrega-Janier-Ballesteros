using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data.SQLite;
using System.IO;
using loginavicola.Model;

namespace loginavicola.Database
{
    public class ReportesDatabase
    {
        private readonly string connectionString;
        private readonly string dbPath;

        public ReportesDatabase()
        {
            dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "sistema_avicola.db");
            connectionString = $"Data Source={dbPath};Version=3;";
        }

        // OBTENER CLASIFICACIONES
        public List<ClasificacionReporte> ObtenerClasificaciones()
        {
            var clasificaciones = new List<ClasificacionReporte>();

            try
            {
                using (var connection = new SQLiteConnection(connectionString))
                {
                    connection.Open();
                    string query = @"
                        SELECT * FROM Clasificacion 
                        ORDER BY Fecha DESC";

                    using (var command = new SQLiteCommand(query, connection))
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            clasificaciones.Add(new ClasificacionReporte
                            {
                                IdClasificacion = Convert.ToInt32(reader["IdClasificacion"]),
                                Fecha = Convert.ToDateTime(reader["Fecha"]),
                                Recolector = reader["Recolector"]?.ToString() ?? string.Empty,
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
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error al obtener clasificaciones: {ex.Message}");
            }

            return clasificaciones;
        }

        // OBTENER LOTES
        public List<Lote> ObtenerLotes()
        {
            var lotes = new List<Lote>();

            try
            {
                using (var connection = new SQLiteConnection(connectionString))
                {
                    connection.Open();
                    string query = "SELECT * FROM Lote ORDER BY FechaIncorporacion DESC";

                    using (var command = new SQLiteCommand(query, connection))
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            lotes.Add(new Lote
                            {
                                IdLote = Convert.ToInt32(reader["IdLote"]),
                                Raza = reader["Raza"]?.ToString() ?? string.Empty,
                                CantidadGallinas = Convert.ToInt32(reader["CantidadGallinas"]),
                                FechaIncorporacion = Convert.ToDateTime(reader["FechaIncorporacion"]),
                                GranjaOrigen = reader["GranjaOrigen"]?.ToString() ?? string.Empty,
                                Estado = reader["Estado"]?.ToString() ?? string.Empty,
                                Observaciones = reader["Observaciones"]?.ToString() ?? string.Empty
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error al obtener lotes: {ex.Message}");
            }

            return lotes;
        }

        // OBTENER CONSUMOS (ALIMENTACIÓN)
        public List<Consumo> ObtenerConsumos()
        {
            var consumos = new List<Consumo>();

            try
            {
                using (var connection = new SQLiteConnection(connectionString))
                {
                    connection.Open();
                    string query = @"
                        SELECT 
                            c.*,
                            a.Nombre as NombreAlimento
                        FROM Consumo c
                        LEFT JOIN Alimento a ON c.IdAlimento = a.IdAlimento
                        ORDER BY c.FechaConsumo DESC";

                    using (var command = new SQLiteCommand(query, connection))
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            consumos.Add(new Consumo
                            {
                                IdConsumo = Convert.ToInt32(reader["IdConsumo"]),
                                FechaConsumo = Convert.ToDateTime(reader["FechaConsumo"]),
                                IdLoteGallinas = Convert.ToInt32(reader["IdLoteGallinas"]),
                                IdAlimento = Convert.ToInt32(reader["IdAlimento"]),
                                CantidadConsumida = Convert.ToDecimal(reader["CantidadConsumida"]),
                                UnidadMedida = reader["UnidadMedida"]?.ToString() ?? string.Empty,
                                Turno = reader["Turno"]?.ToString() ?? string.Empty,
                                Observaciones = reader["Observaciones"]?.ToString() ?? string.Empty,
                                NombreAlimento = reader["NombreAlimento"]?.ToString() ?? string.Empty
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error al obtener consumos: {ex.Message}");
            }

            return consumos;
        }

        // OBTENER ALIMENTOS (INVENTARIO)
        public List<Alimento> ObtenerAlimentos()
        {
            var alimentos = new List<Alimento>();

            try
            {
                using (var connection = new SQLiteConnection(connectionString))
                {
                    connection.Open();
                    string query = "SELECT * FROM Alimento ORDER BY Nombre";

                    using (var command = new SQLiteCommand(query, connection))
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            alimentos.Add(new Alimento
                            {
                                IdAlimento = Convert.ToInt32(reader["IdAlimento"]),
                                Nombre = reader["Nombre"]?.ToString() ?? string.Empty,
                                StockDisponible = (int)Convert.ToDecimal(reader["StockDisponible"])
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error al obtener alimentos: {ex.Message}");
            }

            return alimentos;
        }

        // OBTENER PRODUCCIÓN
        public List<ProduccionReporte> ObtenerProduccion()
        {
            var produccion = new List<ProduccionReporte>();

            try
            {
                using (var connection = new SQLiteConnection(connectionString))
                {
                    connection.Open();

                    // Crear tabla temporal si no existe
                    string createTable = @"
                        CREATE TABLE IF NOT EXISTS Produccion (
                            IdProduccion INTEGER PRIMARY KEY AUTOINCREMENT,
                            Fecha DATE NOT NULL,
                            IdLote INTEGER NOT NULL,
                            CantidadHuevos INTEGER NOT NULL,
                            PorcentajeProduccion DECIMAL(5,2),
                            Observaciones TEXT,
                            FOREIGN KEY (IdLote) REFERENCES Lote(IdLote)
                        )";

                    using (var cmd = new SQLiteCommand(createTable, connection))
                    {
                        cmd.ExecuteNonQuery();
                    }

                    string query = @"
                        SELECT 
                            p.IdProduccion,
                            p.Fecha,
                            p.IdLote,
                            l.Raza,
                            p.CantidadHuevos,
                            p.PorcentajeProduccion,
                            p.Observaciones
                        FROM Produccion p
                        LEFT JOIN Lote l ON p.IdLote = l.IdLote
                        ORDER BY p.Fecha DESC";

                    using (var command = new SQLiteCommand(query, connection))
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            produccion.Add(new ProduccionReporte
                            {
                                IdProduccion = Convert.ToInt32(reader["IdProduccion"]),
                                Fecha = Convert.ToDateTime(reader["Fecha"]),
                                IdLote = Convert.ToInt32(reader["IdLote"]),
                                Raza = reader["Raza"]?.ToString() ?? string.Empty,
                                CantidadHuevos = Convert.ToInt32(reader["CantidadHuevos"]),
                                PorcentajeProduccion = reader["PorcentajeProduccion"] != DBNull.Value
                                    ? Convert.ToDecimal(reader["PorcentajeProduccion"]) : 0,
                                Observaciones = reader["Observaciones"]?.ToString() ?? string.Empty
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error al obtener producción: {ex.Message}");
            }

            return produccion;
        }
    }
}
