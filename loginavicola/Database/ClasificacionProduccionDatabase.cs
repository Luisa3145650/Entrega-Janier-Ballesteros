using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Windows;
using loginavicola.Model;

namespace loginavicola.Database
{
    public class ClasificacionProduccionDatabase
    {
        private readonly string connectionString;

        public ClasificacionProduccionDatabase()
        {
            string dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "sistema_avicola.db");
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
                            IdClasificacion   INTEGER PRIMARY KEY AUTOINCREMENT,
                            Fecha             DATE         NOT NULL,
                            Hora              VARCHAR(20)  NOT NULL,
                            Recolector        VARCHAR(200) NOT NULL,
                            TipoClasificacion VARCHAR(50)  NOT NULL,
                            Jumbo             INTEGER DEFAULT 0,
                            AAA               INTEGER DEFAULT 0,
                            AA                INTEGER DEFAULT 0,
                            A                 INTEGER DEFAULT 0,
                            B                 INTEGER DEFAULT 0,
                            C                 INTEGER DEFAULT 0,
                            Peso              REAL    DEFAULT 0,
                            Volumen           REAL    DEFAULT 0,
                            Total             INTEGER DEFAULT 1,
                            Observaciones     TEXT
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

        public List<ClasificacionProduccion> ObtenerHistorial(int limite = 50)
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
                                    C = Convert.ToInt32(reader["C"]),
                                    Observaciones = reader["Observaciones"]?.ToString()
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex.Message); }
            return lista;
        }

        public bool InsertarClasificacion(ClasificacionProduccion c)
        {
            try
            {
                using (var connection = new SQLiteConnection(connectionString))
                {
                    connection.Open();
                    string query = @"INSERT INTO ClasificacionProduccion (Fecha, Hora, Recolector, TipoClasificacion, Jumbo, AAA, AA, A, B, C, Total, Observaciones) 
                                     VALUES (@Fecha, @Hora, @Recolector, @Tipo, @Jumbo, @AAA, @AA, @A, @B, @C, @Total, @Obs)";

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
                        command.Parameters.AddWithValue("@Obs", c.Observaciones ?? (object)DBNull.Value);

                        return command.ExecuteNonQuery() > 0;
                    }
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex.Message); return false; }
        }

        public void RegistrarHuevoIndividual(string categoria, double peso, double volumen)
        {
            try
            {
                using (var connection = new SQLiteConnection(connectionString))
                {
                    connection.Open();
                    string query = @"INSERT INTO ClasificacionProduccion (Fecha, Hora, Recolector, TipoClasificacion, Jumbo, AAA, AA, A, B, C, Peso, Volumen, Total) 
                                     VALUES (@Fecha, @Hora, 'Sistema Vision', 'Automatica', @Jumbo, @AAA, @AA, @A, @B, @C, @Peso, @Vol, 1)";

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

        public List<ProduccionResumen> ObtenerProduccionPorCategorias()
        {
            var stats = new List<ProduccionResumen>();
            try
            {
                using (var connection = new SQLiteConnection(connectionString))
                {
                    connection.Open();
                    // Consulta corregida para sumar las categorías del día actual
                    string query = @"SELECT SUM(Jumbo), SUM(AAA), SUM(AA), SUM(A), SUM(B), SUM(C) 
                                     FROM ClasificacionProduccion 
                                     WHERE DATE(Fecha) = DATE('now', 'localtime')";

                    using (var command = new SQLiteCommand(query, connection))
                    using (var reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            stats.Add(new ProduccionResumen { Categoria = "Jumbo", Cantidad = reader.IsDBNull(0) ? 0 : Convert.ToInt32(reader[0]) });
                            stats.Add(new ProduccionResumen { Categoria = "AAA", Cantidad = reader.IsDBNull(1) ? 0 : Convert.ToInt32(reader[1]) });
                            stats.Add(new ProduccionResumen { Categoria = "AA", Cantidad = reader.IsDBNull(2) ? 0 : Convert.ToInt32(reader[2]) });
                            stats.Add(new ProduccionResumen { Categoria = "A", Cantidad = reader.IsDBNull(3) ? 0 : Convert.ToInt32(reader[3]) });
                            stats.Add(new ProduccionResumen { Categoria = "B", Cantidad = reader.IsDBNull(4) ? 0 : Convert.ToInt32(reader[4]) });
                            stats.Add(new ProduccionResumen { Categoria = "C", Cantidad = reader.IsDBNull(5) ? 0 : Convert.ToInt32(reader[5]) });
                        }
                    }
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex.Message); }
            return stats;
        }

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
        }

        // NUEVO MÉTODO PARA PRODUCCIÓN DE LOS ÚLTIMOS 7 DÍAS
        public List<ProduccionDiaria> ObtenerProduccionUltimos7Dias()
        {
            var lista = new List<ProduccionDiaria>();

            try
            {
                using (var connection = new SQLiteConnection(connectionString))
                {
                    connection.Open();
                    string query = @"
                        SELECT 
                            CAST(strftime('%w', Fecha) AS INTEGER) as DiaSemanaNum,
                            CASE CAST(strftime('%w', Fecha) AS INTEGER)
                                WHEN 0 THEN 'Dom'
                                WHEN 1 THEN 'Lun'
                                WHEN 2 THEN 'Mar'
                                WHEN 3 THEN 'Mié'
                                WHEN 4 THEN 'Jue'
                                WHEN 5 THEN 'Vie'
                                WHEN 6 THEN 'Sáb'
                            END as DiaSemana,
                            SUM(Total) as Cantidad
                        FROM ClasificacionProduccion
                        WHERE Fecha >= DATE('now', '-7 days')
                        GROUP BY DATE(Fecha)
                        ORDER BY DiaSemanaNum";

                    using (var command = new SQLiteCommand(query, connection))
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            lista.Add(new ProduccionDiaria
                            {
                                DiaSemanaNum = Convert.ToInt32(reader["DiaSemanaNum"]),
                                DiaSemana = reader["DiaSemana"].ToString(),
                                Cantidad = Convert.ToInt32(reader["Cantidad"])
                            });
                        }
                    }
                }

                // Completar los días que faltan con 0
                var diasCompletos = new List<ProduccionDiaria>();
                string[] nombresDias = { "Dom", "Lun", "Mar", "Mié", "Jue", "Vie", "Sáb" };
                for (int i = 0; i < 7; i++)
                {
                    var existente = lista.FirstOrDefault(l => l.DiaSemanaNum == i);
                    if (existente != null)
                    {
                        diasCompletos.Add(existente);
                    }
                    else
                    {
                        diasCompletos.Add(new ProduccionDiaria
                        {
                            DiaSemanaNum = i,
                            DiaSemana = nombresDias[i],
                            Cantidad = 0
                        });
                    }
                }

                return diasCompletos;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error al obtener producción semanal: {ex.Message}");
                // Retornar 7 días con ceros
                var diasPorDefecto = new List<ProduccionDiaria>();
                string[] nombres = { "Dom", "Lun", "Mar", "Mié", "Jue", "Vie", "Sáb" };
                for (int i = 0; i < 7; i++)
                {
                    diasPorDefecto.Add(new ProduccionDiaria
                    {
                        DiaSemanaNum = i,
                        DiaSemana = nombres[i],
                        Cantidad = 0
                    });
                }
                return diasPorDefecto;
            }
        }
    }

    // Clase auxiliar para producción diaria
    public class ProduccionDiaria
    {
        public int DiaSemanaNum { get; set; }
        public string DiaSemana { get; set; }
        public int Cantidad { get; set; }
    }
}