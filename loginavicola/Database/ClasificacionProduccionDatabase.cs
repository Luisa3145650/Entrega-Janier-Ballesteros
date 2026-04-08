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
                    // Dentro de ClasificacionProduccionDatabase.cs -> Método CrearTabla()
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
                               Peso            REAL DEFAULT 0,    -- <--- NUEVA COLUMNA
                               Volumen         REAL DEFAULT 0,    -- <--- NUEVA COLUMNA
                               Total           INTEGER DEFAULT 1,
                               Observaciones   TEXT
                           )";

                    using (var command = new SQLiteCommand(createTable, connection))
                        command.ExecuteNonQuery();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al crear tabla: {ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public void RegistrarHuevoIndividual(string categoria, double peso, double volumen)
        {
            try
            {
                using (var connection = new SQLiteConnection(connectionString))
                {
                    connection.Open();

                    // Mapeo de categorías a columnas (1 para la categoría detectada, 0 para las demás)
                    int jumbo = (categoria == "Jumbo") ? 1 : 0;
                    int aaa = (categoria == "AAA") ? 1 : 0;
                    int aa = (categoria == "AA") ? 1 : 0;
                    int a = (categoria == "A") ? 1 : 0;
                    int b = (categoria == "B") ? 1 : 0;
                    int c = (categoria == "C") ? 1 : 0;

                    // IMPORTANTE: El nombre de la tabla debe ser ClasificacionProduccion
                    string query = @"
                           INSERT INTO ClasificacionProduccion 
                           (Fecha, Hora, Recolector, TipoClasificacion, Jumbo, AAA, AA, A, B, C, Peso, Volumen, Total)
                           VALUES 
                           (@Fecha, @Hora, 'Sistema Vision', 'Automatica', @Jumbo, @AAA, @AA, @A, @B, @C, @Peso, @Vol, 1)";

                    using (var command = new SQLiteCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@Fecha", DateTime.Now.ToString("yyyy-MM-dd"));
                        command.Parameters.AddWithValue("@Hora", DateTime.Now.ToString("HH:mm:ss")); // HH en mayúscula para formato 24h
                        command.Parameters.AddWithValue("@Jumbo", jumbo);
                        command.Parameters.AddWithValue("@AAA", aaa);
                        command.Parameters.AddWithValue("@AA", aa);
                        command.Parameters.AddWithValue("@A", a);
                        command.Parameters.AddWithValue("@B", b);
                        command.Parameters.AddWithValue("@C", c);
                        command.Parameters.AddWithValue("@Peso", peso);
                        command.Parameters.AddWithValue("@Vol", volumen);

                        command.ExecuteNonQuery();
                    }

                    // Actualizamos inventario general
                    var invDb = new InventarioDatabase();
                    invDb.SumarStockDesdeProduccion(categoria, 1);

                    System.Diagnostics.Debug.WriteLine($"✅ Huevo {categoria} ({peso}g) guardado correctamente.");
                }
            }
            catch (Exception ex)
            {
                // Esto te mostrará en la consola de Visual Studio si falta alguna columna
                System.Diagnostics.Debug.WriteLine($"❌ ERROR CRÍTICO SQLITE: {ex.Message}");
            }
        }

        public bool InsertarClasificacion(ClasificacionProduccion clasificacion)
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
                        (@Fecha, @Hora, @Recolector, @TipoClasificacion, @Jumbo, @AAA, @AA, @A, @B, @C, @Total, @Observaciones)";

                    using (var command = new SQLiteCommand(query, connection))
                    {
                     
                        command.Parameters.AddWithValue("@Fecha", clasificacion.Fecha.Date.ToString("yyyy-MM-dd"));
                        command.Parameters.AddWithValue("@Hora", clasificacion.Hora.ToString(@"hh\:mm\:ss"));
                        command.Parameters.AddWithValue("@Recolector", clasificacion.Recolector);
                        command.Parameters.AddWithValue("@TipoClasificacion", clasificacion.TipoClasificacion);
                        command.Parameters.AddWithValue("@Jumbo", clasificacion.Jumbo);
                        command.Parameters.AddWithValue("@AAA", clasificacion.AAA);
                        command.Parameters.AddWithValue("@AA", clasificacion.AA);
                        command.Parameters.AddWithValue("@A", clasificacion.A);
                        command.Parameters.AddWithValue("@B", clasificacion.B);
                        command.Parameters.AddWithValue("@C", clasificacion.C);
                        command.Parameters.AddWithValue("@Total", clasificacion.Total);
                        command.Parameters.AddWithValue("@Observaciones", clasificacion.Observaciones ?? string.Empty);

                        command.ExecuteNonQuery();

                        // NUEVO: Al guardar clasificación, actualizar inventario automáticamente
                        var invDb = new InventarioDatabase();
                        if (clasificacion.Jumbo > 0) invDb.SumarStockDesdeProduccion("Jumbo", clasificacion.Jumbo);
                        if (clasificacion.AAA > 0) invDb.SumarStockDesdeProduccion("AAA", clasificacion.AAA);
                        if (clasificacion.AA > 0) invDb.SumarStockDesdeProduccion("AA", clasificacion.AA);
                        if (clasificacion.A > 0) invDb.SumarStockDesdeProduccion("A", clasificacion.A);
                        if (clasificacion.B > 0) invDb.SumarStockDesdeProduccion("B", clasificacion.B);
                        if (clasificacion.C > 0) invDb.SumarStockDesdeProduccion("C", clasificacion.C);
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al guardar: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        public List<ClasificacionProduccion> ObtenerClasificacionesRecientes(int cantidad = 50)
        {
            var clasificaciones = new List<ClasificacionProduccion>();

            try
            {
                using (var connection = new SQLiteConnection(connectionString))
                {
                    connection.Open();
                    string query = $@"
                        SELECT * FROM ClasificacionProduccion 
                        ORDER BY Fecha DESC, Hora DESC 
                        LIMIT {cantidad}";

                    using (var command = new SQLiteCommand(query, connection))
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {

                            TimeSpan hora = ParsearHora(reader["Hora"]?.ToString());

                            
                            DateTime fecha = ParsearFecha(reader["Fecha"]?.ToString());

                            clasificaciones.Add(new ClasificacionProduccion
                            {
                                IdClasificacion = Convert.ToInt32(reader["IdClasificacion"]),
                                Fecha = fecha,
                                Hora = hora,
                                Recolector = reader["Recolector"]?.ToString() ?? string.Empty,
                                TipoClasificacion = reader["TipoClasificacion"]?.ToString() ?? string.Empty,
                                Jumbo = Convert.ToInt32(reader["Jumbo"]),
                                AAA = Convert.ToInt32(reader["AAA"]),
                                AA = Convert.ToInt32(reader["AA"]),
                                A = Convert.ToInt32(reader["A"]),
                                B = Convert.ToInt32(reader["B"]),
                                C = Convert.ToInt32(reader["C"]),
                                Total = Convert.ToInt32(reader["Total"]),
                                Observaciones = reader["Observaciones"]?.ToString() ?? string.Empty
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error al obtener clasificaciones:\n{ex.Message}\n\nDetalles: {ex.StackTrace}");
            }

            return clasificaciones;
        }

     
        private TimeSpan ParsearHora(string? horaString)
        {
            if (string.IsNullOrWhiteSpace(horaString))
                return TimeSpan.Zero;

            if (TimeSpan.TryParse(horaString, out TimeSpan horaDirecta))
                return horaDirecta;


            if (DateTime.TryParse(horaString, out DateTime fechaHora))
                return fechaHora.TimeOfDay;

            System.Diagnostics.Debug.WriteLine($"[WARN ParsearHora] No se pudo parsear: '{horaString}'");
            return TimeSpan.Zero;
        }


        private DateTime ParsearFecha(string? fechaString)
        {
            if (string.IsNullOrWhiteSpace(fechaString))
                return DateTime.Today;

            if (DateTime.TryParse(fechaString, out DateTime fecha))
                return fecha;

            System.Diagnostics.Debug.WriteLine($"[WARN ParsearFecha] No se pudo parsear: '{fechaString}'");
            return DateTime.Today;
        }

        public int ObtenerProduccionHoy()
        {
            try
            {
                using (var connection = new SQLiteConnection(connectionString))
                {
                    connection.Open();
                    
                    string query = @"
                        SELECT SUM(Total) 
                        FROM ClasificacionProduccion 
                        WHERE DATE(Fecha) = DATE('now', 'localtime')";

                    using (var command = new SQLiteCommand(query, connection))
                    {
                        var result = command.ExecuteScalar();
                        return result != DBNull.Value ? Convert.ToInt32(result) : 0;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ERROR ObtenerProduccionHoy] {ex.Message}");
                return 0;
            }
        }

        public Dictionary<string, int> ObtenerEstadisticasPorCategoria(DateTime fecha)
        {
            var estadisticas = new Dictionary<string, int>
            {
                { "Jumbo", 0 }, { "AAA", 0 }, { "AA", 0 },
                { "A",     0 }, { "B",   0 }, { "C",  0 }
            };

            try
            {
                using (var connection = new SQLiteConnection(connectionString))
                {
                    connection.Open();
                    string query = @"
                        SELECT SUM(Jumbo) as Jumbo, SUM(AAA) as AAA, SUM(AA) as AA,
                               SUM(A) as A, SUM(B) as B, SUM(C) as C
                        FROM ClasificacionProduccion 
                        WHERE DATE(Fecha) = @Fecha";

                    using (var command = new SQLiteCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@Fecha", fecha.Date.ToString("yyyy-MM-dd"));

                        using (var reader = command.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                estadisticas["Jumbo"] = reader["Jumbo"] != DBNull.Value ? Convert.ToInt32(reader["Jumbo"]) : 0;
                                estadisticas["AAA"] = reader["AAA"] != DBNull.Value ? Convert.ToInt32(reader["AAA"]) : 0;
                                estadisticas["AA"] = reader["AA"] != DBNull.Value ? Convert.ToInt32(reader["AA"]) : 0;
                                estadisticas["A"] = reader["A"] != DBNull.Value ? Convert.ToInt32(reader["A"]) : 0;
                                estadisticas["B"] = reader["B"] != DBNull.Value ? Convert.ToInt32(reader["B"]) : 0;
                                estadisticas["C"] = reader["C"] != DBNull.Value ? Convert.ToInt32(reader["C"]) : 0;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al obtener estadísticas: {ex.Message}");
            }

            return estadisticas;
        }
    }
}