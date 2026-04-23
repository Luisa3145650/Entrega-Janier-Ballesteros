using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Windows;
using loginavicola.Model;

namespace loginavicola.Database
{
    public class ClasificacionProduccionDatabase
    {
        private readonly string connectionString;
        private readonly string dbPath;

        public ClasificacionProduccionDatabase()
        {
            dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "sistema_avicola.db");
            connectionString = $"Data Source={dbPath};Version=3;";
            CrearTabla();
        }

        private void CrearTabla()
        {
            try
            {
                using (var connection = new SQLiteConnection(connectionString))
                {
                    connection.Open();
                    string createTable = @"
                        CREATE TABLE IF NOT EXISTS ClasificacionProduccion (
                            IdClasificacion INTEGER PRIMARY KEY AUTOINCREMENT,
                            Fecha           DATE         NOT NULL,
                            Hora            VARCHAR(20)  NOT NULL,
                            Recolector      VARCHAR(200) NOT NULL,
                            TipoClasificacion VARCHAR(50) NOT NULL,
                            Jumbo           INTEGER DEFAULT 0,
                            AAA             INTEGER DEFAULT 0,
                            AA              INTEGER DEFAULT 0,
                            A               INTEGER DEFAULT 0,
                            B               INTEGER DEFAULT 0,
                            C               INTEGER DEFAULT 0,
                            Peso            REAL DEFAULT 0,
                            Volumen         REAL DEFAULT 0,
                            Total           INTEGER DEFAULT 1,
                            Observaciones   TEXT
                        )";

                    using (var command = new SQLiteCommand(createTable, connection))
                        command.ExecuteNonQuery();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al crear tabla producción: {ex.Message}");
            }
        }

<<<<<<< HEAD
        // CORRECCIÓN: Ahora acepta un parámetro opcional para evitar el error de sobrecarga
        public List<ClasificacionProduccion> ObtenerClasificacionesRecientes(int limite = 50)
=======
        // 1. MÉTODO PARA EL HISTORIAL (DataGrid)
        public List<ClasificacionProduccion> ObtenerHistorial(int limite = 50)
>>>>>>> 886cc47dd4978db9f3f1cae3cbad615e35f86466
        {
            var lista = new List<ClasificacionProduccion>();
            try
            {
                using (var connection = new SQLiteConnection(connectionString))
                {
                    connection.Open();
                    string query = "SELECT * FROM ClasificacionProduccion ORDER BY IdClasificacion DESC LIMIT @Limite";
                    using (var command = new SQLiteCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@Limite", limite);
                        using (var reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                lista.Add(new ClasificacionProduccion
                                {
                                    IdClasificacion = Convert.ToInt32(reader["IdClasificacion"]),
                                    Fecha = Convert.ToDateTime(reader["Fecha"]),
                                    Hora = TimeSpan.Parse(reader["Hora"].ToString()),
                                    Recolector = reader["Recolector"].ToString(),
                                    TipoClasificacion = reader["TipoClasificacion"].ToString(),
                                    Total = Convert.ToInt32(reader["Total"]),
                                    Jumbo = Convert.ToInt32(reader["Jumbo"]),
                                    AAA = Convert.ToInt32(reader["AAA"]),
                                    AA = Convert.ToInt32(reader["AA"]),
                                    A = Convert.ToInt32(reader["A"]),
                                    B = Convert.ToInt32(reader["B"]),
<<<<<<< HEAD
                                    C = Convert.ToInt32(reader["C"])
=======
                                    C = Convert.ToInt32(reader["C"]),
                                    Observaciones = reader["Observaciones"]?.ToString()
>>>>>>> 886cc47dd4978db9f3f1cae3cbad615e35f86466
                                });
                            }
                        }
                    }
                }
            }
            catch { }
            return lista;
        }

<<<<<<< HEAD
=======
        // 2. MÉTODO PARA REGISTRO MANUAL
>>>>>>> 886cc47dd4978db9f3f1cae3cbad615e35f86466
        public bool InsertarClasificacion(ClasificacionProduccion c)
        {
            try
            {
                using (var connection = new SQLiteConnection(connectionString))
                {
                    connection.Open();
                    string query = @"
                        INSERT INTO ClasificacionProduccion 
                        (Fecha, Hora, Recolector, TipoClasificacion, Jumbo, AAA, AA, A, B, C, Total, Observaciones)
                        VALUES 
                        (@Fecha, @Hora, @Recolector, @Tipo, @Jumbo, @AAA, @AA, @A, @B, @C, @Total, @Obs)";

                    using (var command = new SQLiteCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@Fecha", c.Fecha.ToString("yyyy-MM-dd"));
                        command.Parameters.AddWithValue("@Hora", c.Hora.ToString(@"hh\:mm\:ss"));
                        command.Parameters.AddWithValue("@Recolector", c.Recolector);
                        command.Parameters.AddWithValue("@Tipo", c.TipoClasificacion);
                        command.Parameters.AddWithValue("@Jumbo", c.Jumbo);
                        command.Parameters.AddWithValue("@AAA", c.AAA);
                        command.Parameters.AddWithValue("@AA", c.AA);
                        command.Parameters.AddWithValue("@A", c.A);
                        command.Parameters.AddWithValue("@B", c.B);
                        command.Parameters.AddWithValue("@C", c.C);
                        command.Parameters.AddWithValue("@Total", c.Total);
                        command.Parameters.AddWithValue("@Obs", c.Observaciones);

<<<<<<< HEAD
                        int result = command.ExecuteNonQuery();
                        return result > 0;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error en DB: {ex.Message}");
                return false;
=======
                        return command.ExecuteNonQuery() > 0;
                    }
                }
>>>>>>> 886cc47dd4978db9f3f1cae3cbad615e35f86466
            }
            catch { return false; }
        }

<<<<<<< HEAD
=======
        // 3. SOLUCIÓN AL ERROR 1: RegistrarHuevoIndividual (Para la Cámara)
>>>>>>> 886cc47dd4978db9f3f1cae3cbad615e35f86466
        public void RegistrarHuevoIndividual(string categoria, double peso, double volumen)
        {
            try
            {
                using (var connection = new SQLiteConnection(connectionString))
                {
                    connection.Open();
                    string query = @"
                        INSERT INTO ClasificacionProduccion 
                        (Fecha, Hora, Recolector, TipoClasificacion, Jumbo, AAA, AA, A, B, C, Peso, Volumen, Total)
                        VALUES 
                        (@Fecha, @Hora, 'Sistema Vision', 'Automatica', @Jumbo, @AAA, @AA, @A, @B, @C, @Peso, @Vol, 1)";
<<<<<<< HEAD
=======

                    using (var command = new SQLiteCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@Fecha", DateTime.Now.ToString("yyyy-MM-dd"));
                        command.Parameters.AddWithValue("@Hora", DateTime.Now.ToString("HH:mm:ss"));
                        command.Parameters.AddWithValue("@Jumbo", categoria == "Jumbo" ? 1 : 0);
                        command.Parameters.AddWithValue("@AAA", categoria == "AAA" ? 1 : 0);
                        command.Parameters.AddWithValue("@AA", categoria == "AA" ? 1 : 0);
                        command.Parameters.AddWithValue("@A", categoria == "A" ? 1 : 0);
                        command.Parameters.AddWithValue("@B", categoria == "B" ? 1 : 0);
                        command.Parameters.AddWithValue("@C", categoria == "C" ? 1 : 0);
                        command.Parameters.AddWithValue("@Peso", peso);
                        command.Parameters.AddWithValue("@Vol", volumen);
                        command.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Error Vision: {ex.Message}"); }
        }

        // 4. SOLUCIÓN AL ERROR 2: ObtenerProduccionPorCategorias (Para el Resumen)
        public List<ProduccionResumen> ObtenerProduccionPorCategorias()
        {
            var stats = new List<ProduccionResumen>();
            try
            {
                using (var connection = new SQLiteConnection(connectionString))
                {
                    connection.Open();
                    string query = @"SELECT SUM(Jumbo), SUM(AAA), SUM(AA), SUM(A), SUM(B), SUM(C) 
                                     FROM ClasificacionProduccion WHERE DATE(Fecha) = DATE('now', 'localtime')";
>>>>>>> 886cc47dd4978db9f3f1cae3cbad615e35f86466

                    using (var command = new SQLiteCommand(query, connection))
                    {
<<<<<<< HEAD
                        command.Parameters.AddWithValue("@Fecha", DateTime.Now.ToString("yyyy-MM-dd"));
                        command.Parameters.AddWithValue("@Hora", DateTime.Now.ToString("HH:mm:ss"));
                        command.Parameters.AddWithValue("@Jumbo", categoria == "Jumbo" ? 1 : 0);
                        command.Parameters.AddWithValue("@AAA", categoria == "AAA" ? 1 : 0);
                        command.Parameters.AddWithValue("@AA", categoria == "AA" ? 1 : 0);
                        command.Parameters.AddWithValue("@A", categoria == "A" ? 1 : 0);
                        command.Parameters.AddWithValue("@B", categoria == "B" ? 1 : 0);
                        command.Parameters.AddWithValue("@C", categoria == "C" ? 1 : 0);
                        command.Parameters.AddWithValue("@Peso", peso);
                        command.Parameters.AddWithValue("@Vol", volumen);
                        command.ExecuteNonQuery();
=======
                        if (reader.Read())
                        {
                            stats.Add(new ProduccionResumen { Categoria = "Jumbo", Cantidad = reader[0] != DBNull.Value ? Convert.ToInt32(reader[0]) : 0 });
                            stats.Add(new ProduccionResumen { Categoria = "AAA", Cantidad = reader[1] != DBNull.Value ? Convert.ToInt32(reader[1]) : 0 });
                            stats.Add(new ProduccionResumen { Categoria = "AA", Cantidad = reader[2] != DBNull.Value ? Convert.ToInt32(reader[2]) : 0 });
                            stats.Add(new ProduccionResumen { Categoria = "A", Cantidad = reader[3] != DBNull.Value ? Convert.ToInt32(reader[3]) : 0 });
                            stats.Add(new ProduccionResumen { Categoria = "B", Cantidad = reader[4] != DBNull.Value ? Convert.ToInt32(reader[4]) : 0 });
                            stats.Add(new ProduccionResumen { Categoria = "C", Cantidad = reader[5] != DBNull.Value ? Convert.ToInt32(reader[5]) : 0 });
                        }
>>>>>>> 886cc47dd4978db9f3f1cae3cbad615e35f86466
                    }
                    new InventarioDatabase().SumarStockDesdeProduccion(categoria, 1);
                }
            }
<<<<<<< HEAD
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Error Vision: {ex.Message}"); }
=======
            catch { }
            return stats;
>>>>>>> 886cc47dd4978db9f3f1cae3cbad615e35f86466
        }

        // 5. Método auxiliar para el total del día
        public int ObtenerProduccionHoy()
        {
            try
            {
                using (var connection = new SQLiteConnection(connectionString))
                {
                    connection.Open();
                    string query = "SELECT SUM(Total) FROM ClasificacionProduccion WHERE DATE(Fecha) = DATE('now', 'localtime')";
                    using (var command = new SQLiteCommand(query, connection))
                    {
                        var result = command.ExecuteScalar();
                        return result != DBNull.Value ? Convert.ToInt32(result) : 0;
                    }
                }
            }
            catch { return 0; }
<<<<<<< HEAD
        }

        public Dictionary<string, int> ObtenerEstadisticasPorCategoria(DateTime fecha)
        {
            var estadisticas = new Dictionary<string, int> { { "Jumbo", 0 }, { "AAA", 0 }, { "AA", 0 }, { "A", 0 }, { "B", 0 }, { "C", 0 } };
            try
            {
                using (var connection = new SQLiteConnection(connectionString))
                {
                    connection.Open();
                    string query = @"SELECT SUM(Jumbo), SUM(AAA), SUM(AA), SUM(A), SUM(B), SUM(C) 
                                     FROM ClasificacionProduccion WHERE DATE(Fecha) = @Fecha";
                    using (var command = new SQLiteCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@Fecha", fecha.ToString("yyyy-MM-dd"));
                        using (var reader = command.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                estadisticas["Jumbo"] = reader[0] != DBNull.Value ? Convert.ToInt32(reader[0]) : 0;
                                estadisticas["AAA"] = reader[1] != DBNull.Value ? Convert.ToInt32(reader[1]) : 0;
                                estadisticas["AA"] = reader[2] != DBNull.Value ? Convert.ToInt32(reader[2]) : 0;
                                estadisticas["A"] = reader[3] != DBNull.Value ? Convert.ToInt32(reader[3]) : 0;
                                estadisticas["B"] = reader[4] != DBNull.Value ? Convert.ToInt32(reader[4]) : 0;
                                estadisticas["C"] = reader[5] != DBNull.Value ? Convert.ToInt32(reader[5]) : 0;
                            }
                        }
                    }
                }
            }
            catch { }
            return estadisticas;
        }

        public List<ProduccionResumen> ObtenerProduccionPorCategorias()
        {
            var stats = ObtenerEstadisticasPorCategoria(DateTime.Now);
            return stats.Where(x => x.Value > 0)
                        .Select(x => new ProduccionResumen { Categoria = x.Key, Cantidad = x.Value })
                        .ToList();
        }
    }

    // CLASE DE APOYO CORREGIDA (Ubicada dentro del namespace para ser encontrada)
    public class ProduccionResumen
    {
        public string Categoria { get; set; }
        public int Cantidad { get; set; }
    }

    public class ProduccionResumen
    {
        public string Categoria { get; set; }
        public int Cantidad { get; set; }
    }
}