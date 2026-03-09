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
    public class VentasDatabase
    {
        private readonly string connectionString;
        private readonly string dbPath;

        public VentasDatabase()
        {
            dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "sistema_avicola.db");
            connectionString = $"Data Source={dbPath};Version=3;";

            CrearTablaVentas();
        }

        private void CrearTablaVentas()
        {
            try
            {
                using (var connection = new SQLiteConnection(connectionString))
                {
                    connection.Open();

                    string createTable = @"
                        CREATE TABLE IF NOT EXISTS Ventas (
                            IdVenta INTEGER PRIMARY KEY AUTOINCREMENT,
                            Fecha DATE NOT NULL,
                            Cliente VARCHAR(200) NOT NULL,
                            TipoVenta VARCHAR(100) NOT NULL,
                            Categoria VARCHAR(100) NOT NULL,
                            Cantidad INTEGER DEFAULT 0,
                            CostoTotal DECIMAL(10,2) DEFAULT 0,
                            Estado VARCHAR(50) DEFAULT 'Pendiente',
                            Observaciones TEXT
                        )";

                    using (var command = new SQLiteCommand(createTable, connection))
                    {
                        command.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al crear tabla Ventas: {ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public List<Venta> ObtenerTodasVentas()
        {
            var ventas = new List<Venta>();

            try
            {
                using (var connection = new SQLiteConnection(connectionString))
                {
                    connection.Open();
                    string query = @"
                        SELECT IdVenta, Fecha, Cliente, TipoVenta, Categoria,
                               Cantidad, CostoTotal, Estado, Observaciones
                        FROM Ventas
                        ORDER BY Fecha DESC";

                    using (var command = new SQLiteCommand(query, connection))
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            ventas.Add(new Venta
                            {
                                IdVenta = Convert.ToInt32(reader["IdVenta"]),
                                Fecha = Convert.ToDateTime(reader["Fecha"]),
                                Cliente = reader["Cliente"]?.ToString() ?? string.Empty,
                                TipoVenta = reader["TipoVenta"]?.ToString() ?? string.Empty,
                                Categoria = reader["Categoria"]?.ToString() ?? string.Empty,
                                Cantidad = Convert.ToInt32(reader["Cantidad"]),
                                CostoTotal = Convert.ToDecimal(reader["CostoTotal"]),
                                Estado = reader["Estado"]?.ToString() ?? "Pendiente",
                                Observaciones = reader["Observaciones"]?.ToString() ?? string.Empty
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al obtener ventas: {ex.Message}");
            }

            return ventas;
        }

        public bool InsertarVenta(Venta venta)
        {
            try
            {
                using (var connection = new SQLiteConnection(connectionString))
                {
                    connection.Open();
                    string query = @"
                        INSERT INTO Ventas 
                        (Fecha, Cliente, TipoVenta, Categoria, Cantidad, CostoTotal, Estado, Observaciones)
                        VALUES 
                        (@Fecha, @Cliente, @TipoVenta, @Categoria, @Cantidad, @CostoTotal, @Estado, @Observaciones)";

                    using (var command = new SQLiteCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@Fecha", venta.Fecha);
                        command.Parameters.AddWithValue("@Cliente", venta.Cliente);
                        command.Parameters.AddWithValue("@TipoVenta", venta.TipoVenta);
                        command.Parameters.AddWithValue("@Categoria", venta.Categoria);
                        command.Parameters.AddWithValue("@Cantidad", venta.Cantidad);
                        command.Parameters.AddWithValue("@CostoTotal", venta.CostoTotal);
                        command.Parameters.AddWithValue("@Estado", venta.Estado);
                        command.Parameters.AddWithValue("@Observaciones", venta.Observaciones ?? string.Empty);

                        command.ExecuteNonQuery();
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al guardar venta: {ex.Message}");
                return false;
            }
        }

        public bool ActualizarVenta(Venta venta)
        {
            try
            {
                using (var connection = new SQLiteConnection(connectionString))
                {
                    connection.Open();
                    string query = @"
                        UPDATE Ventas SET 
                            Fecha = @Fecha,
                            Cliente = @Cliente,
                            TipoVenta = @TipoVenta,
                            Categoria = @Categoria,
                            Cantidad = @Cantidad,
                            CostoTotal = @CostoTotal,
                            Estado = @Estado,
                            Observaciones = @Observaciones
                        WHERE IdVenta = @IdVenta";

                    using (var command = new SQLiteCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@IdVenta", venta.IdVenta);
                        command.Parameters.AddWithValue("@Fecha", venta.Fecha);
                        command.Parameters.AddWithValue("@Cliente", venta.Cliente);
                        command.Parameters.AddWithValue("@TipoVenta", venta.TipoVenta);
                        command.Parameters.AddWithValue("@Categoria", venta.Categoria);
                        command.Parameters.AddWithValue("@Cantidad", venta.Cantidad);
                        command.Parameters.AddWithValue("@CostoTotal", venta.CostoTotal);
                        command.Parameters.AddWithValue("@Estado", venta.Estado);
                        command.Parameters.AddWithValue("@Observaciones", venta.Observaciones ?? string.Empty);

                        command.ExecuteNonQuery();
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al actualizar venta: {ex.Message}");
                return false;
            }
        }

        public bool EliminarVenta(int idVenta)
        {
            try
            {
                using (var connection = new SQLiteConnection(connectionString))
                {
                    connection.Open();
                    string query = "DELETE FROM Ventas WHERE IdVenta = @IdVenta";

                    using (var command = new SQLiteCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@IdVenta", idVenta);
                        command.ExecuteNonQuery();
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al eliminar venta: {ex.Message}");
                return false;
            }
        }

        // ESTADÍSTICAS
        public int ObtenerTotalEntregas()
        {
            try
            {
                using (var connection = new SQLiteConnection(connectionString))
                {
                    connection.Open();
                    string query = "SELECT COUNT(*) FROM Ventas";

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

        public int ObtenerPorEstado(string estado)
        {
            try
            {
                using (var connection = new SQLiteConnection(connectionString))
                {
                    connection.Open();
                    string query = "SELECT COUNT(*) FROM Ventas WHERE Estado = @Estado";

                    using (var command = new SQLiteCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@Estado", estado);
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