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

        public ClasificacionProduccionDatabase()
        {
            string dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "sistema_avicola.db");
            connectionString = $"Data Source={dbPath};Version=3;Journal Mode=WAL;BusyTimeout=5000;";
            CrearTabla();
        }

        private void CrearTabla()
        {
            try
            {
                using (var connection = new SQLiteConnection(connectionString))
                {
                    connection.Open();
                    string createTable = "CREATE TABLE IF NOT EXISTS ClasificacionProduccion (" +
                        "IdClasificacion INTEGER PRIMARY KEY AUTOINCREMENT, " +
                        "IdLote INTEGER NOT NULL, " +
                        "Fecha DATE NOT NULL, " +
                        "HoraInicio TEXT, " +
                        "HoraFin TEXT, " +
                        "Recolector TEXT, " +
                        "TipoClasificacion TEXT, " +
                        "EstadoSesion TEXT DEFAULT 'Abierta', " +
                        "Jumbo INTEGER DEFAULT 0, " +
                        "AAA INTEGER DEFAULT 0, " +
                        "AA INTEGER DEFAULT 0, " +
                        "A INTEGER DEFAULT 0, " +
                        "B INTEGER DEFAULT 0, " +
                        "C INTEGER DEFAULT 0, " +
                        "Peso REAL DEFAULT 0, " +
                        "Volumen REAL DEFAULT 0, " +
                        "Total INTEGER DEFAULT 0, " +
                        "Observaciones TEXT" +
                        ")";
                    using (var command = new SQLiteCommand(createTable, connection))
                        command.ExecuteNonQuery();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al crear tabla producción: {ex.Message}");
            }
        }

        /// <summary>
        /// Inserta el registro consolidado final de un lote clasificado.
        /// </summary>
        public bool InsertarClasificacion(ClasificacionProduccion c)
        {
            try
            {
                using (var connection = new SQLiteConnection(connectionString))
                {
                    connection.Open();

                    // Aseguramos la suma total exacta según el conteo de categorías
                    int totalCalculado = c.Jumbo + c.AAA + c.AA + c.A + c.B + c.C;

                    string query = "INSERT INTO ClasificacionProduccion " +
                        "(IdLote, Fecha, HoraInicio, HoraFin, Recolector, TipoClasificacion, EstadoSesion, Jumbo, AAA, AA, A, B, C, Total, Observaciones) " +
                        "VALUES (@IdLote, @Fecha, @HoraInicio, @HoraFin, @Recolector, @Tipo, 'Finalizada', @Jumbo, @AAA, @AA, @A, @B, @C, @Total, @Obs)";

                    using (var command = new SQLiteCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@IdLote", c.IdLote > 0 ? c.IdLote : 1);
                        command.Parameters.AddWithValue("@Fecha", c.Fecha != default ? c.Fecha.ToString("yyyy-MM-dd") : DateTime.Now.ToString("yyyy-MM-dd"));
                        command.Parameters.AddWithValue("@HoraInicio", string.IsNullOrEmpty(c.HoraInicio) ? DateTime.Now.ToString("HH:mm:ss") : c.HoraInicio);
                        command.Parameters.AddWithValue("@HoraFin", DateTime.Now.ToString("HH:mm:ss"));
                        command.Parameters.AddWithValue("@Recolector", string.IsNullOrEmpty(c.Recolector) ? "Sistema Visión" : c.Recolector);
                        command.Parameters.AddWithValue("@Tipo", string.IsNullOrEmpty(c.TipoClasificacion) ? "Automática" : c.TipoClasificacion);
                        command.Parameters.AddWithValue("@Jumbo", c.Jumbo);
                        command.Parameters.AddWithValue("@AAA", c.AAA);
                        command.Parameters.AddWithValue("@AA", c.AA);
                        command.Parameters.AddWithValue("@A", c.A);
                        command.Parameters.AddWithValue("@B", c.B);
                        command.Parameters.AddWithValue("@C", c.C);
                        command.Parameters.AddWithValue("@Total", totalCalculado);
                        command.Parameters.AddWithValue("@Obs", c.Observaciones ?? (object)DBNull.Value);

                        return command.ExecuteNonQuery() > 0;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error al guardar clasificación consolidada: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Guarda o actualiza los conteos de una sesión activa al momento de finalizar el lote.
        /// </summary>
        public bool FinalizarSesionConConteos(int idClasificacion, int jumbo, int aaa, int aa, int a, int b, int c, string observaciones)
        {
            try
            {
                using (var connection = new SQLiteConnection(connectionString))
                {
                    connection.Open();
                    int totalCalculado = jumbo + aaa + aa + a + b + c;

                    string sql = "UPDATE ClasificacionProduccion " +
                        "SET HoraFin = @HoraFin, EstadoSesion = 'Finalizada', Jumbo = @Jumbo, AAA = @AAA, AA = @AA, A = @A, B = @B, C = @C, Total = @Total, Observaciones = @Obs " +
                        "WHERE IdClasificacion = @Id";

                    using (var cmd = new SQLiteCommand(sql, connection))
                    {
                        cmd.Parameters.AddWithValue("@HoraFin", DateTime.Now.ToString("HH:mm:ss"));
                        cmd.Parameters.AddWithValue("@Jumbo", jumbo);
                        cmd.Parameters.AddWithValue("@AAA", aaa);
                        cmd.Parameters.AddWithValue("@AA", aa);
                        cmd.Parameters.AddWithValue("@A", a);
                        cmd.Parameters.AddWithValue("@B", b);
                        cmd.Parameters.AddWithValue("@C", c);
                        cmd.Parameters.AddWithValue("@Total", totalCalculado);
                        cmd.Parameters.AddWithValue("@Obs", observaciones ?? string.Empty);
                        cmd.Parameters.AddWithValue("@Id", idClasificacion);

                        return cmd.ExecuteNonQuery() > 0;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al finalizar sesión: {ex.Message}");
                return false;
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
                                    HoraInicio = reader["HoraInicio"]?.ToString() ?? string.Empty,
                                    HoraFin = reader["HoraFin"]?.ToString() ?? string.Empty,
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

        public List<ProduccionResumen> ObtenerProduccionPorCategorias()
        {
            var stats = new List<ProduccionResumen>();
            try
            {
                using (var connection = new SQLiteConnection(connectionString))
                {
                    connection.Open();
                    string query = "SELECT SUM(Jumbo), SUM(AAA), SUM(AA), SUM(A), SUM(B), SUM(C) FROM ClasificacionProduccion WHERE DATE(Fecha) = DATE('now','localtime')";

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

        public List<ProduccionDiaria> ObtenerProduccionUltimos7Dias()
        {
            var lista = new List<ProduccionDiaria>();

            try
            {
                using (var connection = new SQLiteConnection(connectionString))
                {
                    connection.Open();
                    string query = "SELECT CAST(strftime('%w', Fecha) AS INTEGER) as DiaSemanaNum, " +
                        "CASE CAST(strftime('%w', Fecha) AS INTEGER) " +
                        "WHEN 0 THEN 'Dom' " +
                        "WHEN 1 THEN 'Lun' " +
                        "WHEN 2 THEN 'Mar' " +
                        "WHEN 3 THEN 'Mie' " +
                        "WHEN 4 THEN 'Jue' " +
                        "WHEN 5 THEN 'Vie' " +
                        "WHEN 6 THEN 'Sab' " +
                        "END as DiaSemana, " +
                        "SUM(Total) as Cantidad " +
                        "FROM ClasificacionProduccion " +
                        "WHERE Fecha >= DATE('now', '-7 days') " +
                        "GROUP BY DATE(Fecha) " +
                        "ORDER BY DiaSemanaNum";

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

                var diasCompletos = new List<ProduccionDiaria>();
                string[] nombresDias = { "Dom", "Lun", "Mar", "Mie", "Jue", "Vie", "Sab" };
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
                var diasPorDefecto = new List<ProduccionDiaria>();
                string[] nombres = { "Dom", "Lun", "Mar", "Mie", "Jue", "Vie", "Sab" };
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

        public int CrearSesion(int idLote, string recolector, string tipoClasificacion)
        {
            try
            {
                using (var connection = new SQLiteConnection(connectionString))
                {
                    connection.Open();

                    string sql = "INSERT INTO ClasificacionProduccion " +
                        "(IdLote, Fecha, HoraInicio, Recolector, TipoClasificacion, EstadoSesion, Jumbo, AAA, AA, A, B, C, Total, Observaciones) " +
                        "VALUES (@IdLote, @Fecha, @HoraInicio, @Recolector, @Tipo, 'Abierta', 0, 0, 0, 0, 0, 0, 0, ''); " +
                        "SELECT last_insert_rowid();";

                    using (var cmd = new SQLiteCommand(sql, connection))
                    {
                        cmd.Parameters.AddWithValue("@IdLote", idLote);
                        cmd.Parameters.AddWithValue("@Fecha", DateTime.Now.ToString("yyyy-MM-dd"));
                        cmd.Parameters.AddWithValue("@HoraInicio", DateTime.Now.ToString("HH:mm:ss"));
                        cmd.Parameters.AddWithValue("@Recolector", recolector);
                        cmd.Parameters.AddWithValue("@Tipo", tipoClasificacion);

                        return Convert.ToInt32(cmd.ExecuteScalar());
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
                return 0;
            }
        }
    }

    public class ProduccionDiaria
    {
        public int DiaSemanaNum { get; set; }
        public string DiaSemana { get; set; }
        public int Cantidad { get; set; }
    }

    public class ProduccionResumen
    {
        public string Categoria { get; set; }
        public int Cantidad { get; set; }
    }
}