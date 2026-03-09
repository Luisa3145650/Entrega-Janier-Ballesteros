using System;
using System.Data;
using System.Data.SQLite;
using System.IO;
using System.Collections.Generic;

namespace loginavícola.Model
{

    public class Alimento
    {
        public string IdAlimento { get; set; } = string.Empty;
        public string Nombre { get; set; } = string.Empty;
        public string TipoAlimento { get; set; } = string.Empty;
        public string Marca { get; set; } = string.Empty;
        public decimal CostoPorUnidad { get; set; }
        public string UnidadMedida { get; set; } = string.Empty;
        public string Comentarios { get; set; } = string.Empty;
    }
    
    public class ConsumoAlimento
    {
        public string IdConsumo { get; set; } = string.Empty;
        public string IdLoteGallinas { get; set; } = string.Empty;
        public string IdAlimento { get; set; } = string.Empty;
        public DateTime FechaConsumo { get; set; } = DateTime.Now;
        public decimal CantidadConsumida { get; set; }
        public string UnidadMedida { get; set; } = string.Empty;
        public string Turno { get; set; } = string.Empty;
        public string Observaciones { get; set; } = string.Empty;
        public decimal CostoTotal { get; set; }
    }
    

    public class Lote
    {
        public string IdLote { get; set; } = string.Empty;
        public string Raza { get; set; } = string.Empty;
        public int CantidadGallinas { get; set; }
        public string Estado { get; set; } = string.Empty;
        public DateTime FechaIncorporacion { get; set; } = DateTime.Now;
        public string GranjaOrigen { get; set; } = string.Empty;
        public string Observaciones { get; set; } = string.Empty;
    }

    public class DatabaseModel
    {
        private string connectionString;

        public DatabaseModel()
        {
            string databasePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "sistema_avícola.db");
            connectionString = $"Data Source={databasePath};Version=3;";

            Console.WriteLine($"Conectando a: {databasePath}");
            InicializarBaseDeDatos();
            InicializarTablaLotes();
            InicializarTablaAlimentos();        // NUEVA LÍNEA
            InicializarTablaConsumos();         // NUEVA LÍNEA

            InsertarAlimentosEjemplo();         // NUEVA LÍNEA
        }

        public SQLiteConnection GetConnection()
        {
            return new SQLiteConnection(connectionString);
        }

        public DataTable ExecuteQuery(string query, SQLiteParameter[]? parameters = null)
        {
            var dataTable = new DataTable();

            try
            {
                using (var connection = GetConnection())
                {
                    connection.Open();
                    using (var command = new SQLiteCommand(query, connection))
                    {
                        if (parameters != null)
                        {
                            command.Parameters.AddRange(parameters);
                        }

                        using (var adapter = new SQLiteDataAdapter(command))
                        {
                            adapter.Fill(dataTable);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error en ExecuteQuery: {ex.Message}");
            }

            return dataTable;
        }

        public int ExecuteNonQuery(string query, SQLiteParameter[]? parameters = null)
        {
            try
            {
                using (var connection = GetConnection())
                {
                    connection.Open();
                    using (var command = new SQLiteCommand(query, connection))
                    {
                        if (parameters != null)
                        {
                            command.Parameters.AddRange(parameters);
                        }
                        return command.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error en ExecuteNonQuery: {ex.Message}");
                return -1;
            }
        }

        // Método para verificar si la conexión funciona
        public bool TestConnection()
        {
            try
            {
                using (var connection = GetConnection())
                {
                    connection.Open();
                    return true;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error de conexión: {ex.Message}");
                return false;
            }
        }

        // Método para obtener un solo valor
        public object? ExecuteScalar(string query, SQLiteParameter[]? parameters = null)
        {
            try
            {
                using (var connection = GetConnection())
                {
                    connection.Open();
                    using (var command = new SQLiteCommand(query, connection))
                    {
                        if (parameters != null)
                        {
                            command.Parameters.AddRange(parameters);
                        }
                        return command.ExecuteScalar();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error en ExecuteScalar: {ex.Message}");
                return null;
            }
        }

        // Método para inicializar la base de datos
        public void InicializarBaseDeDatos()
        {
            try
            {
                // Crear tabla de usuarios si no existe
                string createTableQuery = @"
                    CREATE TABLE IF NOT EXISTS usuarios (
                        id INTEGER PRIMARY KEY AUTOINCREMENT,
                        username TEXT NOT NULL UNIQUE,
                        password TEXT NOT NULL,
                        nombre TEXT NOT NULL,
                        rol TEXT NOT NULL
                    )";

                ExecuteNonQuery(createTableQuery);

                // Verificar si ya existen usuarios
                string checkUsersQuery = "SELECT COUNT(*) FROM usuarios";
                var userCount = ExecuteScalar(checkUsersQuery);

                // Si no hay usuarios, insertar algunos de prueba
                if (userCount != null && Convert.ToInt32(userCount) == 0)
                {
                    string insertUsersQuery = @"
                        INSERT INTO usuarios (username, password, nombre, rol) VALUES 
                        ('admin', '1234', 'Administrador Principal', 'admin'),
                        ('usuario', '1234', 'Usuario Regular', 'user'),
                        ('veterinario', '1234', 'Veterinario', 'vet')";

                    ExecuteNonQuery(insertUsersQuery);
                    Console.WriteLine("Usuarios de prueba creados exitosamente");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error inicializando base de datos: {ex.Message}");
            }
        }



        public void InicializarTablaLotes()
        {
            string query = @"
                CREATE TABLE IF NOT EXISTS lotes (
                     IdLote INTEGER PRIMARY KEY AUTOINCREMENT,
                     Raza TEXT,
                     CantidadGallinas INTEGER,
                     Estado TEXT,
                     FechaIncorporacion DATETIME,
                     GranjaOrigen TEXT,
                     Observaciones TEXT
                )";
            ExecuteNonQuery(query);
        }

        public void InicializarTablaAlimentos()
        {
            string query = @"
        CREATE TABLE IF NOT EXISTS alimentos (
            IdAlimento TEXT PRIMARY KEY,
            Nombre TEXT NOT NULL,
            TipoAlimento TEXT NOT NULL,
            Marca TEXT,
            CostoPorUnidad REAL NOT NULL,
            UnidadMedida TEXT NOT NULL,
            Comentarios TEXT
        )";
            ExecuteNonQuery(query);
        }

        public void InicializarTablaConsumos()
        {
            string query = @"
        CREATE TABLE IF NOT EXISTS consumos_alimento (
            IdConsumo TEXT PRIMARY KEY,
            IdLoteGallinas TEXT NOT NULL,
            IdAlimento TEXT NOT NULL,
            FechaConsumo TEXT NOT NULL,
            CantidadConsumida REAL NOT NULL,
            UnidadMedida TEXT NOT NULL,
            Turno TEXT NOT NULL,
            Observaciones TEXT,
            CostoTotal REAL NOT NULL,
            FOREIGN KEY (IdLoteGallinas) REFERENCES lotes(IdLote),
            FOREIGN KEY (IdAlimento) REFERENCES alimentos(IdAlimento)
        )";
            ExecuteNonQuery(query);
        }

        private void InsertarAlimentosEjemplo()
        {
            try
            {
                string checkQuery = "SELECT COUNT(*) FROM alimentos";
                var count = ExecuteScalar(checkQuery);

                if (count != null && Convert.ToInt32(count) == 0)
                {
                    // Insertar alimentos de ejemplo
                    var alimentosEjemplo = new[]
                    {
                new Alimento {
                    IdAlimento = "A001",
                    Nombre = "Concentrado Inicio",
                    TipoAlimento = "Iniciador",
                    Marca = "Purina",
                    CostoPorUnidad = 25.50m,
                    UnidadMedida = "kg"
                },
                new Alimento {
                    IdAlimento = "A002",
                    Nombre = "Concentrado Crecimiento",
                    TipoAlimento = "Crecimiento",
                    Marca = "Purina",
                    CostoPorUnidad = 22.80m,
                    UnidadMedida = "kg"
                },
                new Alimento {
                    IdAlimento = "A003",
                    Nombre = "Concentrado Postura",
                    TipoAlimento = "Postura",
                    Marca = "Purina",
                    CostoPorUnidad = 28.30m,
                    UnidadMedida = "kg"
                }
            };

                    foreach (var alimento in alimentosEjemplo)
                    {
                        InsertarAlimento(alimento);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error insertando alimentos ejemplo: {ex.Message}");
            }
        }
        public bool InsertarAlimento(Alimento alimento)
        {
            if (alimento == null) return false;

            try
            {
                string query = @"
            INSERT INTO alimentos (IdAlimento, Nombre, TipoAlimento, Marca, CostoPorUnidad, UnidadMedida, Comentarios)
            VALUES (@IdAlimento, @Nombre, @TipoAlimento, @Marca, @CostoPorUnidad, @UnidadMedida, @Comentarios)";

                SQLiteParameter[] parameters = {
            new SQLiteParameter("@IdAlimento", alimento.IdAlimento ?? ""),
            new SQLiteParameter("@Nombre", alimento.Nombre ?? ""),
            new SQLiteParameter("@TipoAlimento", alimento.TipoAlimento ?? ""),
            new SQLiteParameter("@Marca", alimento.Marca ?? ""),
            new SQLiteParameter("@CostoPorUnidad", alimento.CostoPorUnidad),
            new SQLiteParameter("@UnidadMedida", alimento.UnidadMedida ?? ""),
            new SQLiteParameter("@Comentarios", alimento.Comentarios ?? "")
        };

                int result = ExecuteNonQuery(query, parameters);
                return result > 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error insertando alimento: {ex.Message}");
                return false;
            }
        }

        public List<Alimento> ObtenerTodosLosAlimentos()
        {
            var alimentos = new List<Alimento>();

            try
            {
                string query = "SELECT * FROM alimentos ORDER BY Nombre";
                DataTable dataTable = ExecuteQuery(query);

                if (dataTable != null)
                {
                    foreach (DataRow row in dataTable.Rows)
                    {
                        var alimento = new Alimento
                        {
                            IdAlimento = SafeGetString(row, "IdAlimento"),
                            Nombre = SafeGetString(row, "Nombre"),
                            TipoAlimento = SafeGetString(row, "TipoAlimento"),
                            Marca = SafeGetString(row, "Marca"),
                            UnidadMedida = SafeGetString(row, "UnidadMedida"),
                            Comentarios = SafeGetString(row, "Comentarios")
                        };

                        if (decimal.TryParse(SafeGetString(row, "CostoPorUnidad"), out decimal costo))
                        {
                            alimento.CostoPorUnidad = costo;
                        }

                        alimentos.Add(alimento);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error obteniendo alimentos: {ex.Message}");
            }

            return alimentos;
        }
        // 6. Agrega los métodos CRUD para Consumos
        public bool InsertarConsumo(ConsumoAlimento consumo)
        {
            if (consumo == null) return false;

            try
            {
                string query = @"
            INSERT INTO consumos_alimento (IdConsumo, IdLoteGallinas, IdAlimento, FechaConsumo, CantidadConsumida, UnidadMedida, Turno, Observaciones, CostoTotal)
            VALUES (@IdConsumo, @IdLoteGallinas, @IdAlimento, @FechaConsumo, @CantidadConsumida, @UnidadMedida, @Turno, @Observaciones, @CostoTotal)";

                SQLiteParameter[] parameters = {
            new SQLiteParameter("@IdConsumo", consumo.IdConsumo ?? ""),
            new SQLiteParameter("@IdLoteGallinas", consumo.IdLoteGallinas ?? ""),
            new SQLiteParameter("@IdAlimento", consumo.IdAlimento ?? ""),
            new SQLiteParameter("@FechaConsumo", consumo.FechaConsumo.ToString("yyyy-MM-dd")),
            new SQLiteParameter("@CantidadConsumida", consumo.CantidadConsumida),
            new SQLiteParameter("@UnidadMedida", consumo.UnidadMedida ?? ""),
            new SQLiteParameter("@Turno", consumo.Turno ?? ""),
            new SQLiteParameter("@Observaciones", consumo.Observaciones ?? ""),
            new SQLiteParameter("@CostoTotal", consumo.CostoTotal)
        };

                int result = ExecuteNonQuery(query, parameters);
                return result > 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error insertando consumo: {ex.Message}");
                return false;
            }
        }

        public List<ConsumoAlimento> ObtenerTodosLosConsumos()
        {
            var consumos = new List<ConsumoAlimento>();

            try
            {
                string query = @"
            SELECT c.* 
            FROM consumos_alimento c
            ORDER BY c.FechaConsumo DESC, c.IdConsumo DESC";

                DataTable dataTable = ExecuteQuery(query);

                if (dataTable != null)
                {
                    foreach (DataRow row in dataTable.Rows)
                    {
                        var consumo = new ConsumoAlimento
                        {
                            IdConsumo = SafeGetString(row, "IdConsumo"),
                            IdLoteGallinas = SafeGetString(row, "IdLoteGallinas"),
                            IdAlimento = SafeGetString(row, "IdAlimento"),
                            UnidadMedida = SafeGetString(row, "UnidadMedida"),
                            Turno = SafeGetString(row, "Turno"),
                            Observaciones = SafeGetString(row, "Observaciones")
                        };

                        if (DateTime.TryParse(SafeGetString(row, "FechaConsumo"), out DateTime fecha))
                        {
                            consumo.FechaConsumo = fecha;
                        }

                        if (decimal.TryParse(SafeGetString(row, "CantidadConsumida"), out decimal cantidad))
                        {
                            consumo.CantidadConsumida = cantidad;
                        }

                        if (decimal.TryParse(SafeGetString(row, "CostoTotal"), out decimal costoTotal))
                        {
                            consumo.CostoTotal = costoTotal;
                        }

                        consumos.Add(consumo);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error obteniendo consumos: {ex.Message}");
            }

            return consumos;
        }

        // 7. Agrega el método para obtener lotes activos
        public List<Lote> ObtenerLotesActivos()
        {
            var lotes = new List<Lote>();

            try
            {
                string query = "SELECT * FROM lotes WHERE Estado IN ('Activo', 'En producción') ORDER BY IdLote";
                DataTable dataTable = ExecuteQuery(query);

                if (dataTable != null)
                {
                    foreach (DataRow row in dataTable.Rows)
                    {
                        var lote = new Lote
                        {
                            IdLote = SafeGetString(row, "IdLote"),
                            Raza = SafeGetString(row, "Raza"),
                            Estado = SafeGetString(row, "Estado"),
                            GranjaOrigen = SafeGetString(row, "GranjaOrigen"),
                            Observaciones = SafeGetString(row, "Observaciones")
                        };

                        if (int.TryParse(SafeGetString(row, "CantidadGallinas"), out int cantidad))
                        {
                            lote.CantidadGallinas = cantidad;
                        }

                        if (DateTime.TryParse(SafeGetString(row, "FechaIncorporacion"), out DateTime fecha))
                        {
                            lote.FechaIncorporacion = fecha;
                        }

                        lotes.Add(lote);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error obteniendo lotes activos: {ex.Message}");
            }

            return lotes;
        }

        // 8. Agrega el método para calcular costo total
        public decimal CalcularCostoTotal(string idAlimento, decimal cantidad)
        {
            try
            {
                string query = "SELECT CostoPorUnidad FROM alimentos WHERE IdAlimento = @IdAlimento";
                SQLiteParameter[] parameters = {
            new SQLiteParameter("@IdAlimento", idAlimento)
        };

                var costoPorUnidad = ExecuteScalar(query, parameters);
                if (costoPorUnidad != null && decimal.TryParse(costoPorUnidad.ToString(), out decimal costo))
                {
                    return costo * cantidad;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error calculando costo total: {ex.Message}");
            }

            return 0;
        }

        // Métodos específicos para la gestión de lotes
        public bool InsertarLote(Lote lote)
        {
            try
            {
                using (var connection = new SQLiteConnection(connectionString))
                {
                    connection.Open();
                    string query = "INSERT INTO Lotes (Raza, CantidadGallinas, Estado, FechaIncorporacion, GranjaOrigen, Observaciones) " +
                                   "VALUES (@Raza, @CantidadGallinas, @Estado, @FechaIncorporacion, @GranjaOrigen, @Observaciones)";
                    using (var command = new SQLiteCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@Raza", lote.Raza);
                        command.Parameters.AddWithValue("@CantidadGallinas", lote.CantidadGallinas);
                        command.Parameters.AddWithValue("@Estado", lote.Estado);
                        command.Parameters.AddWithValue("@FechaIncorporacion", lote.FechaIncorporacion);
                        command.Parameters.AddWithValue("@GranjaOrigen", lote.GranjaOrigen);
                        command.Parameters.AddWithValue("@Observaciones", lote.Observaciones);

                        command.ExecuteNonQuery();
                    }
                }
                return true;
            }
            catch
            {
                return false;
            }
        }


        public List<Lote> ObtenerTodosLosLotes()
        {
            var lotes = new List<Lote>();

            try
            {
                string query = "SELECT * FROM lotes ORDER BY FechaIncorporacion DESC";
                DataTable dataTable = ExecuteQuery(query);

                if (dataTable != null)
                {
                    foreach (DataRow row in dataTable.Rows)
                    {
                        var lote = new Lote
                        {
                            IdLote = SafeGetString(row, "IdLote"),
                            Raza = SafeGetString(row, "Raza"),
                            Estado = SafeGetString(row, "Estado"),
                            GranjaOrigen = SafeGetString(row, "GranjaOrigen"),
                            Observaciones = SafeGetString(row, "Observaciones")
                        };

                        // Manejar conversiones seguras para números y fechas
                        if (int.TryParse(SafeGetString(row, "CantidadGallinas"), out int cantidad))
                        {
                            lote.CantidadGallinas = cantidad;
                        }

                        if (DateTime.TryParse(SafeGetString(row, "FechaIncorporacion"), out DateTime fecha))
                        {
                            lote.FechaIncorporacion = fecha;
                        }

                        lotes.Add(lote);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error obteniendo lotes: {ex.Message}");
            }

            return lotes;
        }

        private string SafeGetString(DataRow row, string columnName)
        {
            try
            {
                return row[columnName]?.ToString() ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        public bool ActualizarLote(Lote lote)
        {
            if (lote == null) return false;

            try
            {
                string query = @"
                    UPDATE lotes 
                    SET Raza = @Raza, 
                        CantidadGallinas = @CantidadGallinas, 
                        Estado = @Estado, 
                        FechaIncorporacion = @FechaIncorporacion, 
                        GranjaOrigen = @GranjaOrigen, 
                        Observaciones = @Observaciones
                    WHERE IdLote = @IdLote";

                SQLiteParameter[] parameters = {
                    new SQLiteParameter("@IdLote", lote.IdLote ?? ""),
                    new SQLiteParameter("@Raza", lote.Raza ?? ""),
                    new SQLiteParameter("@CantidadGallinas", lote.CantidadGallinas),
                    new SQLiteParameter("@Estado", lote.Estado ?? ""),
                    new SQLiteParameter("@FechaIncorporacion", lote.FechaIncorporacion.ToString("yyyy-MM-dd")),
                    new SQLiteParameter("@GranjaOrigen", lote.GranjaOrigen ?? ""),
                    new SQLiteParameter("@Observaciones", lote.Observaciones ?? "")
                };

                int result = ExecuteNonQuery(query, parameters);
                return result > 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error actualizando lote: {ex.Message}");
                return false;
            }
        }

        public bool EliminarLote(string idLote)
        {
            if (string.IsNullOrEmpty(idLote)) return false;

            try
            {
                string query = "DELETE FROM lotes WHERE IdLote = @IdLote";
                SQLiteParameter[] parameters = {
            new SQLiteParameter("@IdLote", idLote)
        };

                int result = ExecuteNonQuery(query, parameters);
                return result > 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error eliminando lote: {ex.Message}");
                return false; // ← IMPORTANTE: Cambiar a 'false' aquí
            }
        }

        public Lote? ObtenerLotePorId(string idLote)
        {
            if (string.IsNullOrEmpty(idLote)) return null;

            try
            {
                string query = "SELECT * FROM lotes WHERE IdLote = @IdLote";
                SQLiteParameter[] parameters = {
                    new SQLiteParameter("@IdLote", idLote)
                };

                DataTable dataTable = ExecuteQuery(query, parameters);

                if (dataTable != null && dataTable.Rows.Count > 0)
                {
                    DataRow row = dataTable.Rows[0];
                    var lote = new Lote
                    {
                        IdLote = SafeGetString(row, "IdLote"),
                        Raza = SafeGetString(row, "Raza"),
                        Estado = SafeGetString(row, "Estado"),
                        GranjaOrigen = SafeGetString(row, "GranjaOrigen"),
                        Observaciones = SafeGetString(row, "Observaciones")
                    };

                    // Manejar conversiones seguras para números y fechas
                    if (int.TryParse(SafeGetString(row, "CantidadGallinas"), out int cantidad))
                    {
                        lote.CantidadGallinas = cantidad;
                    }

                    if (DateTime.TryParse(SafeGetString(row, "FechaIncorporacion"), out DateTime fecha))
                    {
                        lote.FechaIncorporacion = fecha;
                    }

                    return lote;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error obteniendo lote por ID: {ex.Message}");
            }

            return null;
        }
    }


}