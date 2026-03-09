using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using loginavicola.Model;
using System.Data.SQLite;
using System.IO;
using System.Windows;

namespace loginavicola.Database
{
    public class DiagnosticoDatabase
    {
        private readonly string connectionString;
        private readonly string dbPath;

        public DiagnosticoDatabase()
        {
            // Usar la misma base de datos
            dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "sistema_avicola.db");
            connectionString = $"Data Source={dbPath};Version=3;";

            CrearTablaDiagnostico();
        }

        private void CrearTablaDiagnostico()
        {
            try
            {
                using (var connection = new SQLiteConnection(connectionString))
                {
                    connection.Open();

                    string createTable = @"
                        CREATE TABLE IF NOT EXISTS Diagnostico (
                            IdDiagnostico INTEGER PRIMARY KEY AUTOINCREMENT,
                            FechaDiagnostico DATE NOT NULL,
                            Tipo VARCHAR(50) NOT NULL,
                            IdLote INTEGER NOT NULL,
                            DiagnosticoMedico TEXT NOT NULL,
                            Tratamiento TEXT,
                            GallinasAfectadas INTEGER DEFAULT 0,
                            Veterinario VARCHAR(100),
                            Observaciones TEXT,
                            Estado VARCHAR(20) DEFAULT 'Activo',
                            FOREIGN KEY (IdLote) REFERENCES Lote(IdLote)
                        )";

                    using (var command = new SQLiteCommand(createTable, connection))
                    {
                        command.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al crear tabla Diagnostico: {ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // OBTENER TODOS LOS DIAGNÓSTICOS
        public List<Diagnostico> ObtenerTodosDiagnosticos()
        {
            var diagnosticos = new List<Diagnostico>();

            try
            {
                using (var connection = new SQLiteConnection(connectionString))
                {
                    connection.Open();
                    string query = @"
                        SELECT IdDiagnostico, FechaDiagnostico, Tipo, IdLote, 
                               DiagnosticoMedico, Tratamiento, GallinasAfectadas,
                               Veterinario, Observaciones, Estado
                        FROM Diagnostico
                        ORDER BY FechaDiagnostico DESC";

                    using (var command = new SQLiteCommand(query, connection))
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            diagnosticos.Add(new Diagnostico
                            {
                                IdDiagnostico = Convert.ToInt32(reader["IdDiagnostico"]),
                                FechaDiagnostico = Convert.ToDateTime(reader["FechaDiagnostico"]),
                                Tipo = reader["Tipo"]?.ToString() ?? string.Empty,
                                IdLote = Convert.ToInt32(reader["IdLote"]),
                                DiagnosticoMedico = reader["DiagnosticoMedico"]?.ToString() ?? string.Empty,
                                Tratamiento = reader["Tratamiento"]?.ToString() ?? string.Empty,
                                GallinasAfectadas = Convert.ToInt32(reader["GallinasAfectadas"]),
                                Veterinario = reader["Veterinario"]?.ToString() ?? string.Empty,
                                Observaciones = reader["Observaciones"]?.ToString() ?? string.Empty,
                                Estado = reader["Estado"]?.ToString() ?? "Activo"
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al obtener diagnósticos: {ex.Message}");
            }

            return diagnosticos;
        }

        // INSERTAR NUEVO DIAGNÓSTICO
        public bool InsertarDiagnostico(Diagnostico diagnostico)
        {
            try
            {
                using (var connection = new SQLiteConnection(connectionString))
                {
                    connection.Open();
                    string query = @"
                        INSERT INTO Diagnostico 
                        (FechaDiagnostico, Tipo, IdLote, DiagnosticoMedico, Tratamiento, 
                         GallinasAfectadas, Veterinario, Observaciones, Estado)
                        VALUES 
                        (@FechaDiagnostico, @Tipo, @IdLote, @DiagnosticoMedico, @Tratamiento,
                         @GallinasAfectadas, @Veterinario, @Observaciones, @Estado)";

                    using (var command = new SQLiteCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@FechaDiagnostico", diagnostico.FechaDiagnostico);
                        command.Parameters.AddWithValue("@Tipo", diagnostico.Tipo);
                        command.Parameters.AddWithValue("@IdLote", diagnostico.IdLote);
                        command.Parameters.AddWithValue("@DiagnosticoMedico", diagnostico.DiagnosticoMedico);
                        command.Parameters.AddWithValue("@Tratamiento", diagnostico.Tratamiento ?? string.Empty);
                        command.Parameters.AddWithValue("@GallinasAfectadas", diagnostico.GallinasAfectadas);
                        command.Parameters.AddWithValue("@Veterinario", diagnostico.Veterinario ?? string.Empty);
                        command.Parameters.AddWithValue("@Observaciones", diagnostico.Observaciones ?? string.Empty);
                        command.Parameters.AddWithValue("@Estado", diagnostico.Estado ?? "Activo");

                        command.ExecuteNonQuery();
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al guardar diagnóstico: {ex.Message}");
                return false;
            }
        }

        // ACTUALIZAR DIAGNÓSTICO
        public bool ActualizarDiagnostico(Diagnostico diagnostico)
        {
            try
            {
                using (var connection = new SQLiteConnection(connectionString))
                {
                    connection.Open();
                    string query = @"
                        UPDATE Diagnostico SET 
                            FechaDiagnostico = @FechaDiagnostico,
                            Tipo = @Tipo,
                            IdLote = @IdLote,
                            DiagnosticoMedico = @DiagnosticoMedico,
                            Tratamiento = @Tratamiento,
                            GallinasAfectadas = @GallinasAfectadas,
                            Veterinario = @Veterinario,
                            Observaciones = @Observaciones,
                            Estado = @Estado
                        WHERE IdDiagnostico = @IdDiagnostico";

                    using (var command = new SQLiteCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@IdDiagnostico", diagnostico.IdDiagnostico);
                        command.Parameters.AddWithValue("@FechaDiagnostico", diagnostico.FechaDiagnostico);
                        command.Parameters.AddWithValue("@Tipo", diagnostico.Tipo);
                        command.Parameters.AddWithValue("@IdLote", diagnostico.IdLote);
                        command.Parameters.AddWithValue("@DiagnosticoMedico", diagnostico.DiagnosticoMedico);
                        command.Parameters.AddWithValue("@Tratamiento", diagnostico.Tratamiento ?? string.Empty);
                        command.Parameters.AddWithValue("@GallinasAfectadas", diagnostico.GallinasAfectadas);
                        command.Parameters.AddWithValue("@Veterinario", diagnostico.Veterinario ?? string.Empty);
                        command.Parameters.AddWithValue("@Observaciones", diagnostico.Observaciones ?? string.Empty);
                        command.Parameters.AddWithValue("@Estado", diagnostico.Estado ?? "Activo");

                        command.ExecuteNonQuery();
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al actualizar diagnóstico: {ex.Message}");
                return false;
            }
        }

        // ELIMINAR DIAGNÓSTICO
        public bool EliminarDiagnostico(int idDiagnostico)
        {
            try
            {
                using (var connection = new SQLiteConnection(connectionString))
                {
                    connection.Open();
                    string query = "DELETE FROM Diagnostico WHERE IdDiagnostico = @IdDiagnostico";

                    using (var command = new SQLiteCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@IdDiagnostico", idDiagnostico);
                        command.ExecuteNonQuery();
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al eliminar diagnóstico: {ex.Message}");
                return false;
            }
        }

        // ESTADÍSTICAS
        public int ObtenerTotalDiagnosticos()
        {
            try
            {
                using (var connection = new SQLiteConnection(connectionString))
                {
                    connection.Open();
                    string query = "SELECT COUNT(*) FROM Diagnostico";

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

        public int ObtenerCasosActivos()
        {
            try
            {
                using (var connection = new SQLiteConnection(connectionString))
                {
                    connection.Open();
                    string query = "SELECT COUNT(*) FROM Diagnostico WHERE Estado = 'Activo'";

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

        public int ObtenerCasosResueltos()
        {
            try
            {
                using (var connection = new SQLiteConnection(connectionString))
                {
                    connection.Open();
                    string query = "SELECT COUNT(*) FROM Diagnostico WHERE Estado = 'Resuelto'";

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

        public int ObtenerTotalAvesAfectadas()
        {
            try
            {
                using (var connection = new SQLiteConnection(connectionString))
                {
                    connection.Open();
                    string query = "SELECT COALESCE(SUM(GallinasAfectadas), 0) FROM Diagnostico WHERE Estado = 'Activo'";

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
