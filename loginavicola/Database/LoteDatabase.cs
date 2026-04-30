using loginavicola.Model;
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace loginavicola.Database
{
    public class LoteDatabase
    {
        private readonly string connectionString;
        private readonly string dbPath;

        public LoteDatabase()
        {
            // Usar la misma base de datos que ConsumoDatabase
            dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "sistema_avicola.db");
     
            connectionString = $"Data Source={dbPath};Version=3;";

            CrearBaseDeDatos();
            CrearTablaLote();
        }


        private void CrearBaseDeDatos()
        {
            try
            {
                if (!File.Exists(dbPath))
                {
                    // Asegurar que el directorio existe
                    string directory = Path.GetDirectoryName(dbPath);
                    if (!Directory.Exists(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }

                    SQLiteConnection.CreateFile(dbPath);
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error al crear base de datos: {ex.Message}");
            }
        }
        private void CrearTablaLote()
        {
            try
            {
                using (var connection = new SQLiteConnection(connectionString))
                {
                    connection.Open();

                    // 1. ELIMINAR LA TABLA SI EXISTE (Esto borra todos los datos viejos)
                    //string dropTable = "DROP TABLE IF EXISTS Lote;";


                    // 2. VOLVER A CREAR LA TABLA DESDE CERO
                    string createTable = @"
                CREATE TABLE IF NOT EXISTS Lote (
                    IdLote INTEGER PRIMARY KEY AUTOINCREMENT,
                    Raza VARCHAR(100) NOT NULL,
                    CantidadGallinas INTEGER NOT NULL,
                    FechaIncorporacion DATE NOT NULL,
                    GranjaOrigen VARCHAR(200),
                    Estado VARCHAR(50),
                    Observaciones TEXT
                )";

                    using (var commandCreate = new SQLiteCommand(createTable, connection))
                    {
                        commandCreate.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error al reiniciar tabla: {ex.Message}");
            }
        }



        // OBTENER TODOS LOS LOTES
        public List<Lote> ObtenerTodosLosLotes()
        {
            var lotes = new List<Lote>();

            try
            {
                using (var connection = new SQLiteConnection(connectionString))
                {
                    connection.Open();
                    string query = @"
                        SELECT IdLote, Raza, CantidadGallinas, FechaIncorporacion, 
                               GranjaOrigen, Estado, Observaciones
                        FROM Lote
                        ORDER BY FechaIncorporacion DESC";

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

        // INSERTAR NUEVO LOTE
        public bool InsertarLote(Lote lote)
        {
            try
            {
                using (var connection = new SQLiteConnection(connectionString))
                {
                    connection.Open();
                    string query = @"
                INSERT INTO Lote 
                (Raza, CantidadGallinas, FechaIncorporacion, GranjaOrigen, Estado, Observaciones)
                VALUES 
                (@Raza, @CantidadGallinas, @FechaIncorporacion, @GranjaOrigen, @Estado, @Observaciones)";

                    using (var command = new SQLiteCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@Raza", lote.Raza);
                        command.Parameters.AddWithValue("@CantidadGallinas", lote.CantidadGallinas);
                        command.Parameters.AddWithValue("@FechaIncorporacion", lote.FechaIncorporacion);
                        command.Parameters.AddWithValue("@GranjaOrigen", lote.GranjaOrigen ?? string.Empty);
                        command.Parameters.AddWithValue("@Estado", lote.Estado ?? string.Empty);
                        command.Parameters.AddWithValue("@Observaciones", lote.Observaciones ?? string.Empty);

                        command.ExecuteNonQuery();
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error al guardar lote: {ex.Message}");
                return false;
            }
        }

        // ACTUALIZAR LOTE
        public bool ActualizarLote(Lote lote)
        {
            try
            {
                using (var connection = new SQLiteConnection(connectionString))
                {
                    connection.Open();
                    string query = @"
                UPDATE Lote SET 
                    Raza = @Raza,
                    CantidadGallinas = @CantidadGallinas,
                    FechaIncorporacion = @FechaIncorporacion,
                    GranjaOrigen = @GranjaOrigen,
                    Estado = @Estado,
                    Observaciones = @Observaciones
                WHERE IdLote = @IdLote";

                    using (var command = new SQLiteCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@IdLote", lote.IdLote);
                        command.Parameters.AddWithValue("@Raza", lote.Raza);
                        command.Parameters.AddWithValue("@CantidadGallinas", lote.CantidadGallinas);
                        command.Parameters.AddWithValue("@FechaIncorporacion", lote.FechaIncorporacion);
                        command.Parameters.AddWithValue("@GranjaOrigen", lote.GranjaOrigen ?? string.Empty);
                        command.Parameters.AddWithValue("@Estado", lote.Estado ?? string.Empty);
                        command.Parameters.AddWithValue("@Observaciones", lote.Observaciones ?? string.Empty);

                        command.ExecuteNonQuery();
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error al actualizar lote: {ex.Message}");
                return false;
            }
        }

        // ELIMINAR LOTE
        public bool EliminarLote(int idLote)
        {
            try
            {
                using (var connection = new SQLiteConnection(connectionString))
                {
                    connection.Open();
                    string query = "DELETE FROM Lote WHERE IdLote = @IdLote";

                    using (var command = new SQLiteCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@IdLote", idLote);
                        command.ExecuteNonQuery();
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error al eliminar lote: {ex.Message}");
                return false;
            }
        }

        // OBTENER TOTAL DE LOTES
        public int ObtenerTotalLotes()
        {
            try
            {
                using (var connection = new SQLiteConnection(connectionString))
                {
                    connection.Open();
                    string query = "SELECT COUNT(*) FROM Lote";

                    using (var command = new SQLiteCommand(query, connection))
                    {
                        return Convert.ToInt32(command.ExecuteScalar());
                    }
                }
            }
            catch
            {
                return 0;
            }
        }

        // OBTENER LOTES ACTIVOS
        public int ObtenerLotesActivos()
        {
            try
            {
                using (var connection = new SQLiteConnection(connectionString))
                {
                    connection.Open();
                    string query = "SELECT COUNT(*) FROM Lote WHERE CantidadGallinas > 0";

                    using (var command = new SQLiteCommand(query, connection))
                    {
                        return Convert.ToInt32(command.ExecuteScalar());
                    }
                }
            }
            catch
            {
                return 0;
            }
        }

        // OBTENER TOTAL DE AVES
        public int ObtenerTotalAves()
        {
            try
            {
                using (var connection = new SQLiteConnection(connectionString))
                {
                    connection.Open();
                    string query = "SELECT COALESCE(SUM(CantidadGallinas), 0) FROM Lote";

                    using (var command = new SQLiteCommand(query, connection))
                    {
                        return Convert.ToInt32(command.ExecuteScalar());
                    }
                }
            }
            catch
            {
                return 0;
            }
        }

        // OBTENER TOTAL DE AVES EN PRODUCCIÓN (Estado = Activo)
        public int ObtenerTotalAvesEnProduccion()
        {
            try
            {
                using (var connection = new SQLiteConnection(connectionString))
                {
                    connection.Open();
                    string query = "SELECT COALESCE(SUM(CantidadGallinas), 0) FROM Lote WHERE Estado = 'Activo'";

                    using (var command = new SQLiteCommand(query, connection))
                    {
                        return Convert.ToInt32(command.ExecuteScalar());
                    }
                }
            }
            catch
            {
                return 0;
            }
        }

        // OBTENER TOTAL DE AVES PENSIONADAS (Estado = Pensionado)
        public int ObtenerTotalAvesPensionadas()
        {
            try
            {
                using (var connection = new SQLiteConnection(connectionString))
                {
                    connection.Open();
                    string query = "SELECT COALESCE(SUM(CantidadGallinas), 0) FROM Lote WHERE Estado = 'Pensionado'";

                    using (var command = new SQLiteCommand(query, connection))
                    {
                        return Convert.ToInt32(command.ExecuteScalar());
                    }
                }
            }
            catch
            {
                return 0;
            }
        }
    }


}
