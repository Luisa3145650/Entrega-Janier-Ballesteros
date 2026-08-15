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
    public class InventarioDatabase
    {
        private readonly string connectionString;
        private readonly string dbPath;

        public InventarioDatabase()
        {
            // Usar la misma base de datos
            dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "sistema_avicola.db");
            connectionString = $"Data Source={dbPath};Version=3;";

            CrearTablaInventario();
        }

        private void CrearTablaInventario()
        {
            try
            {
                using (var connection = new SQLiteConnection(connectionString))
                {
                    connection.Open();

                    string createTable = @"
                        CREATE TABLE IF NOT EXISTS Inventario (
                            IdItem INTEGER PRIMARY KEY AUTOINCREMENT,
                            Nombre VARCHAR(200) NOT NULL,
                            Categoria VARCHAR(100) NOT NULL,
                            CostoUnitario DECIMAL(10,2) NOT NULL,
                            Ubicacion VARCHAR(100),
                            FechaCaducidad DATE,
                            StockMinimo INTEGER DEFAULT 0,
                            StockMaximo INTEGER DEFAULT 0,
                            CantidadStock INTEGER DEFAULT 0,
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
                MessageBox.Show($"Error al crear tabla Inventario: {ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // OBTENER TODOS LOS ITEMS
        public List<ItemInventario> ObtenerTodosItems()
        {
            var items = new List<ItemInventario>();

            try
            {
                using (var connection = new SQLiteConnection(connectionString))
                {
                    connection.Open();
                    string query = @"
                        SELECT IdItem, Nombre, Categoria, CostoUnitario, Ubicacion,
                               FechaCaducidad, StockMinimo, StockMaximo, CantidadStock, Observaciones
                        FROM Inventario
                        ORDER BY Nombre";

                    using (var command = new SQLiteCommand(query, connection))
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            items.Add(new ItemInventario
                            {
                                IdItem = Convert.ToInt32(reader["IdItem"]),
                                Nombre = reader["Nombre"]?.ToString() ?? string.Empty,
                                Categoria = reader["Categoria"]?.ToString() ?? string.Empty,
                                CostoUnitario = Convert.ToDecimal(reader["CostoUnitario"]),
                                Ubicacion = reader["Ubicacion"]?.ToString() ?? string.Empty,
                                FechaCaducidad = reader["FechaCaducidad"] != DBNull.Value
                                    ? Convert.ToDateTime(reader["FechaCaducidad"])
                                    : (DateTime?)null,
                                StockMinimo = Convert.ToInt32(reader["StockMinimo"]),
                                StockMaximo = Convert.ToInt32(reader["StockMaximo"]),
                                CantidadStock = Convert.ToInt32(reader["CantidadStock"]),
                                Observaciones = reader["Observaciones"]?.ToString() ?? string.Empty
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al obtener items: {ex.Message}");
            }

            return items;
        }

        // INSERTAR NUEVO ITEM
        public bool InsertarItem(ItemInventario item)
        {
            try
            {
                using (var connection = new SQLiteConnection(connectionString))
                {
                    connection.Open();
                    string query = @"
                        INSERT INTO Inventario 
                        (Nombre, Categoria, CostoUnitario, Ubicacion, FechaCaducidad,
                         StockMinimo, StockMaximo, CantidadStock, Observaciones)
                        VALUES 
                        (@Nombre, @Categoria, @CostoUnitario, @Ubicacion, @FechaCaducidad,
                         @StockMinimo, @StockMaximo, @CantidadStock, @Observaciones)";

                    using (var command = new SQLiteCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@Nombre", item.Nombre);
                        command.Parameters.AddWithValue("@Categoria", item.Categoria);
                        command.Parameters.AddWithValue("@CostoUnitario", item.CostoUnitario);
                        command.Parameters.AddWithValue("@Ubicacion", item.Ubicacion ?? string.Empty);
                        command.Parameters.AddWithValue("@FechaCaducidad",
                            item.FechaCaducidad.HasValue ? (object)item.FechaCaducidad.Value : DBNull.Value);
                        command.Parameters.AddWithValue("@StockMinimo", item.StockMinimo);
                        command.Parameters.AddWithValue("@StockMaximo", item.StockMaximo);
                        command.Parameters.AddWithValue("@CantidadStock", item.CantidadStock);
                        command.Parameters.AddWithValue("@Observaciones", item.Observaciones ?? string.Empty);

                        command.ExecuteNonQuery();
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al guardar item: {ex.Message}");
                return false;
            }
        }

        // ACTUALIZAR ITEM
        public bool ActualizarItem(ItemInventario item)
        {
            try
            {
                using (var connection = new SQLiteConnection(connectionString))
                {
                    connection.Open();
                    string query = @"
                        UPDATE Inventario SET 
                            Nombre = @Nombre,
                            Categoria = @Categoria,
                            CostoUnitario = @CostoUnitario,
                            Ubicacion = @Ubicacion,
                            FechaCaducidad = @FechaCaducidad,
                            StockMinimo = @StockMinimo,
                            StockMaximo = @StockMaximo,
                            CantidadStock = @CantidadStock,
                            Observaciones = @Observaciones
                        WHERE IdItem = @IdItem";

                    using (var command = new SQLiteCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@IdItem", item.IdItem);
                        command.Parameters.AddWithValue("@Nombre", item.Nombre);
                        command.Parameters.AddWithValue("@Categoria", item.Categoria);
                        command.Parameters.AddWithValue("@CostoUnitario", item.CostoUnitario);
                        command.Parameters.AddWithValue("@Ubicacion", item.Ubicacion ?? string.Empty);
                        command.Parameters.AddWithValue("@FechaCaducidad",
                            item.FechaCaducidad.HasValue ? (object)item.FechaCaducidad.Value : DBNull.Value);
                        command.Parameters.AddWithValue("@StockMinimo", item.StockMinimo);
                        command.Parameters.AddWithValue("@StockMaximo", item.StockMaximo);
                        command.Parameters.AddWithValue("@CantidadStock", item.CantidadStock);
                        command.Parameters.AddWithValue("@Observaciones", item.Observaciones ?? string.Empty);

                        command.ExecuteNonQuery();
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al actualizar item: {ex.Message}");
                return false;
            }
        }

        // ELIMINAR ITEM
        public bool EliminarItem(int idItem)
        {
            try
            {
                using (var connection = new SQLiteConnection(connectionString))
                {
                    connection.Open();
                    string query = "DELETE FROM Inventario WHERE IdItem = @IdItem";

                    using (var command = new SQLiteCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@IdItem", idItem);
                        command.ExecuteNonQuery();
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al eliminar item: {ex.Message}");
                return false;
            }
        }

        // ACTUALIZAR STOCK
        public bool ActualizarStock(int idItem, int cantidad, string operacion)
        {
            try
            {
                using (var connection = new SQLiteConnection(connectionString))
                {
                    connection.Open();
                    string query = operacion == "suma"
                        ? "UPDATE Inventario SET CantidadStock = CantidadStock + @Cantidad WHERE IdItem = @IdItem"
                        : "UPDATE Inventario SET CantidadStock = CantidadStock - @Cantidad WHERE IdItem = @IdItem";

                    using (var command = new SQLiteCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@IdItem", idItem);
                        command.Parameters.AddWithValue("@Cantidad", cantidad);
                        command.ExecuteNonQuery();
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al actualizar stock: {ex.Message}");
                return false;
            }
        }

        // ESTADÍSTICAS
        public int ObtenerTotalProductos()
        {
            try
            {
                using (var connection = new SQLiteConnection(connectionString))
                {
                    connection.Open();
                    string query = "SELECT COUNT(*) FROM Inventario";

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

        public int ObtenerStockBajo()
        {
            try
            {
                using (var connection = new SQLiteConnection(connectionString))
                {
                    connection.Open();
                    string query = "SELECT COUNT(*) FROM Inventario WHERE CantidadStock <= StockMinimo";

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

        public int ObtenerStockOptimo()
        {
            try
            {
                using (var connection = new SQLiteConnection(connectionString))
                {
                    connection.Open();
                    string query = "SELECT COUNT(*) FROM Inventario WHERE CantidadStock > StockMinimo AND CantidadStock < StockMaximo";

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

        public decimal ObtenerValorTotal()
        {
            try
            {
                using (var connection = new SQLiteConnection(connectionString))
                {
                    connection.Open();
                    string query = "SELECT COALESCE(SUM(CostoUnitario * CantidadStock), 0) FROM Inventario";

                    using (var command = new SQLiteCommand(query, connection))
                    {
                        return Convert.ToDecimal(command.ExecuteScalar());
                    }
                }
            }
            catch
            {
                return 0;
            }
        }


        // NUEVO: Obtener stock disponible de huevos por categoría de clasificación
        public int ObtenerStockHuevosPorCategoria(string categoria)
        {
            try
            {
                using (var connection = new SQLiteConnection(connectionString))
                {
                    connection.Open();
                    string query = @"
                SELECT COALESCE(SUM(CantidadStock), 0) 
                FROM Inventario 
                WHERE Nombre = 'Huevos' AND Categoria = @Categoria";

                    using (var command = new SQLiteCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@Categoria", categoria);
                        return Convert.ToInt32(command.ExecuteScalar());
                    }
                }
            }
            catch
            {
                return 0;
            }
        }

        // NUEVO: Llamado desde ClasficacionProduccionDatabase al guardar una clasificación
        // Suma huevos al inventario por cada categoría. Si no existe el registro, lo crea.
        public void SumarStockDesdeProduccion(string categoria, int cantidad)
        {
            if (cantidad <= 0) return;

            try
            {
                using (var connection = new SQLiteConnection(connectionString))
                {
                    connection.Open();

                    // Verificar si ya existe un item de Huevos con esa categoría
                    string queryBuscar = @"
                SELECT IdItem FROM Inventario 
                WHERE Nombre = 'Huevos' AND Categoria = @Categoria
                LIMIT 1";

                    int idItem = 0;
                    using (var cmd = new SQLiteCommand(queryBuscar, connection))
                    {
                        cmd.Parameters.AddWithValue("@Categoria", categoria);
                        var result = cmd.ExecuteScalar();
                        if (result != null && result != DBNull.Value)
                            idItem = Convert.ToInt32(result);
                    }

                    if (idItem > 0)
                    {
                        // Ya existe → solo sumar
                        string queryUpdate = @"
                    UPDATE Inventario 
                    SET CantidadStock = CantidadStock + @Cantidad
                    WHERE IdItem = @IdItem";

                        using (var cmd = new SQLiteCommand(queryUpdate, connection))
                        {
                            cmd.Parameters.AddWithValue("@Cantidad", cantidad);
                            cmd.Parameters.AddWithValue("@IdItem", idItem);
                            cmd.ExecuteNonQuery();
                        }
                    }
                    else
                    {
                        // No existe → crear registro automáticamente
                        string queryInsert = @"
                    INSERT INTO Inventario 
                    (Nombre, Categoria, CostoUnitario, Ubicacion, StockMinimo, StockMaximo, CantidadStock, Observaciones)
                    VALUES 
                    ('Huevos', @Categoria, 0, 'Producción', 0, 999999, @Cantidad, 'Generado automáticamente desde producción')";

                        using (var cmd = new SQLiteCommand(queryInsert, connection))
                        {
                            cmd.Parameters.AddWithValue("@Categoria", categoria);
                            cmd.Parameters.AddWithValue("@Cantidad", cantidad);
                            cmd.ExecuteNonQuery();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al actualizar stock desde producción: {ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    

    // NUEVO: Descontar stock al registrar una venta
public void DescontarStockHuevos(string categoria, int cantidad)
        {
            if (cantidad <= 0) return;

            try
            {
                using (var connection = new SQLiteConnection(connectionString))
                {
                    connection.Open();
                    string query = @"
                UPDATE Inventario 
                SET CantidadStock = MAX(0, CantidadStock - @Cantidad)
                WHERE Nombre = 'Huevos' AND Categoria = @Categoria";

                    using (var command = new SQLiteCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@Cantidad", cantidad);
                        command.Parameters.AddWithValue("@Categoria", categoria);
                        command.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al descontar stock: {ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
    }
