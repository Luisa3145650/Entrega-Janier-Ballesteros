using Emgu.CV.Face;
using loginavicola.Model;
using System.Windows;
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Globalization;
using System.IO;
using System.Runtime.ConstrainedExecution;
using static Emgu.CV.VideoCapture;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.TextBox;

namespace loginavicola.Database
{
    public class ConsumoDatabase
    {
        private readonly string connectionString;
        private readonly string dbPath;
        public ConsumoDatabase()
        {
            DatabaseHelper.Inicializar();
            dbPath = DatabaseHelper.DbPath;
            connectionString = DatabaseHelper.ConnectionString;

            CrearBaseDeDatos();
            EliminarFKDeConsumo();
            CrearTablasNecesarias();
        }

        private void EliminarFKDeConsumo()
        {
            try
            {
                using (var connection = new SQLiteConnection(connectionString))
                {
                    connection.Open();

                    // SQLite no permite ALTER TABLE DROP CONSTRAINT
                    // La única forma es recrear la tabla completa

                    // Paso 1: Verificar si la tabla existe
                    string checkTable = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='Consumo'";
                    using (var cmd = new SQLiteCommand(checkTable, connection))
                    {
                        long existe = (long)cmd.ExecuteScalar();
                        if (existe == 0) return; // No existe, nada que migrar
                    }

                    // Paso 2: Verificar si ya fue migrada (si tiene columna NombreAlimento)
                    // y verificar que la FK fue eliminada revisando el SQL de creación
                    string checkSql = "SELECT sql FROM sqlite_master WHERE type='table' AND name='Consumo'";
                    string tableSql = string.Empty;
                    using (var cmd = new SQLiteCommand(checkSql, connection))
                    {
                        tableSql = cmd.ExecuteScalar()?.ToString() ?? string.Empty;
                    }

                    // Si ya tiene NombreAlimento Y no tiene FK de IdAlimento, ya fue migrada
                    if (tableSql.Contains("NombreAlimento") &&
                        !tableSql.Contains("REFERENCES Alimento(IdAlimento)"))
                    {
                        return; // Ya está migrada, no hacer nada
                    }

                    // Paso 3: Migración - recrear tabla sin FK de IdAlimento
                    string migracion = @"
                BEGIN TRANSACTION;

                -- Crear tabla temporal con la nueva estructura
                CREATE TABLE Consumo_Nueva (
                    IdConsumo         INTEGER PRIMARY KEY AUTOINCREMENT,
                    FechaConsumo      DATE NOT NULL,
                    IdLoteGallinas    INTEGER NOT NULL,
                    IdAlimento        INTEGER NOT NULL,
                    NombreAlimento    VARCHAR(200),
                    CantidadConsumida DECIMAL(10,2) NOT NULL,
                    UnidadMedida      VARCHAR(20) NOT NULL DEFAULT 'kg',
                    Turno             VARCHAR(20) DEFAULT 'Semanal',
                    Observaciones     TEXT,
                    NumeroSemana      INTEGER,
                    Año               INTEGER,
                    CantidadGallinas  INTEGER,
                    ConsumoEsperado   DECIMAL(10,2),
                    Merma             DECIMAL(10,2),
                    AlertaMerma       BOOLEAN DEFAULT 0,
                    FOREIGN KEY (IdLoteGallinas) REFERENCES Lote(IdLote)
                );

                -- Copiar datos existentes
                INSERT INTO Consumo_Nueva 
                    (IdConsumo, FechaConsumo, IdLoteGallinas, IdAlimento,
                     CantidadConsumida, UnidadMedida, Turno, Observaciones,
                     NumeroSemana, Año, CantidadGallinas, ConsumoEsperado,
                     Merma, AlertaMerma)
                SELECT 
                    IdConsumo, FechaConsumo, IdLoteGallinas, IdAlimento,
                    CantidadConsumida, UnidadMedida, Turno, Observaciones,
                    NumeroSemana, Año, CantidadGallinas, ConsumoEsperado,
                    Merma, AlertaMerma
                FROM Consumo;

                -- Eliminar tabla vieja
                DROP TABLE Consumo;

                -- Renombrar la nueva
                ALTER TABLE Consumo_Nueva RENAME TO Consumo;

                COMMIT;";

                    // Ejecutar cada sentencia por separado
                    var sentencias = new[]
                    {
                "CREATE TABLE Consumo_Nueva (IdConsumo INTEGER PRIMARY KEY AUTOINCREMENT, FechaConsumo DATE NOT NULL, IdLoteGallinas INTEGER NOT NULL, IdAlimento INTEGER NOT NULL, NombreAlimento VARCHAR(200), CantidadConsumida DECIMAL(10,2) NOT NULL, UnidadMedida VARCHAR(20) NOT NULL DEFAULT 'kg', Turno VARCHAR(20) DEFAULT 'Semanal', Observaciones TEXT, NumeroSemana INTEGER, Año INTEGER, CantidadGallinas INTEGER, ConsumoEsperado DECIMAL(10,2), Merma DECIMAL(10,2), AlertaMerma BOOLEAN DEFAULT 0, FOREIGN KEY (IdLoteGallinas) REFERENCES Lote(IdLote))",

                @"INSERT INTO Consumo_Nueva 
                    (IdConsumo, FechaConsumo, IdLoteGallinas, IdAlimento,
                     CantidadConsumida, UnidadMedida, Turno, Observaciones,
                     NumeroSemana, Año, CantidadGallinas, ConsumoEsperado,
                     Merma, AlertaMerma)
                  SELECT 
                    IdConsumo, FechaConsumo, IdLoteGallinas, IdAlimento,
                    CantidadConsumida, UnidadMedida, Turno, Observaciones,
                    NumeroSemana, Año, CantidadGallinas, ConsumoEsperado,
                    Merma, AlertaMerma
                  FROM Consumo",

                "DROP TABLE Consumo",
                "ALTER TABLE Consumo_Nueva RENAME TO Consumo"
            };

                    using (var transaction = connection.BeginTransaction())
                    {
                        try
                        {
                            foreach (var sentencia in sentencias)
                            {
                                using (var cmd = new SQLiteCommand(sentencia, connection, transaction))
                                    cmd.ExecuteNonQuery();
                            }
                            transaction.Commit();
                        }
                        catch
                        {
                            transaction.Rollback();
                            throw;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Error al migrar tabla Consumo: {ex.Message}\n\n" +
                    "Intente eliminar el archivo 'sistema_avicola.db' y reiniciar la aplicación.",
                    "Error de Migración",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }
        private void CrearBaseDeDatos()
        {
            try
            {
                if (File.Exists(dbPath))
                {
                    try
                    {
                        using (var connection = new SQLiteConnection(connectionString))
                        {
                            connection.Open();
                            connection.Close();
                        }
                    }
                    catch
                    {
                        File.Delete(dbPath);
                        System.Windows.MessageBox.Show("Base de datos corrupta detectada. Se creará una nueva.");
                    }
                }

                if (!File.Exists(dbPath))
                {
                    SQLiteConnection.CreateFile(dbPath);
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error al crear base de datos: {ex.Message}");
            }
        }

        private void CrearTablasNecesarias()
        {
            try
            {
                using (var connection = new SQLiteConnection(connectionString))
                {
                    connection.Open();

                    // ✅ OPCIÓN B: Habilitar FK primero pero luego las eliminamos de Consumo
                    using (var cmd = new SQLiteCommand("PRAGMA foreign_keys = ON;", connection))
                        cmd.ExecuteNonQuery();

                    // Tabla Lote - sin cambios
                    string createLoteTable = @"
                        CREATE TABLE IF NOT EXISTS Lote (
                            IdLote INTEGER PRIMARY KEY AUTOINCREMENT,
                            Raza VARCHAR(100) NOT NULL,
                            CantidadGallinas INTEGER NOT NULL,
                            FechaIncorporacion DATE NOT NULL,
                            GranjaOrigen VARCHAR(200),
                            Estado VARCHAR(50),
                            Observaciones TEXT,
                            CantidadActual INTEGER NOT NULL DEFAULT 0,
                            FechaIngreso DATE
                        )";

                    // Tabla Alimento - se mantiene para compatibilidad
                    string createAlimentoTable = @"
                        CREATE TABLE IF NOT EXISTS Alimento (
                            IdAlimento INTEGER PRIMARY KEY AUTOINCREMENT,
                            Nombre VARCHAR(100) NOT NULL,
                            StockDisponible DECIMAL(10,2) DEFAULT 0
                        )";

                    // ✅ OPCIÓN B: Tabla Consumo SIN FOREIGN KEY en IdAlimento
                    // Se agrega NombreAlimento para guardar el nombre directamente
                    string createConsumoTable = @"
                        CREATE TABLE IF NOT EXISTS Consumo (
                            IdConsumo         INTEGER PRIMARY KEY AUTOINCREMENT,
                            FechaConsumo      DATE NOT NULL,
                            IdLoteGallinas    INTEGER NOT NULL,
                            IdAlimento        INTEGER NOT NULL,
                            NombreAlimento    VARCHAR(200),
                            CantidadConsumida DECIMAL(10,2) NOT NULL,
                            UnidadMedida      VARCHAR(20) NOT NULL DEFAULT 'kg',
                            Turno             VARCHAR(20) DEFAULT 'Semanal',
                            Observaciones     TEXT,
                            NumeroSemana      INTEGER,
                            Año               INTEGER,
                            CantidadGallinas  INTEGER,
                            ConsumoEsperado   DECIMAL(10,2),
                            Merma             DECIMAL(10,2),
                            AlertaMerma       BOOLEAN DEFAULT 0,
                            FOREIGN KEY (IdLoteGallinas) REFERENCES Lote(IdLote)
                            -- ✅ FK de IdAlimento ELIMINADA intencionalmente
                            -- IdAlimento ahora referencia Inventario.IdItem
                        )";

                    using (var command = new SQLiteCommand(createLoteTable, connection))
                        command.ExecuteNonQuery();

                    using (var command = new SQLiteCommand(createAlimentoTable, connection))
                        command.ExecuteNonQuery();

                    // ✅ Si la tabla Consumo ya existe pero sin NombreAlimento, la migramos
                    MigrarTablaConsumoSiEsNecesario(connection);

                    using (var command = new SQLiteCommand(createConsumoTable, connection))
                        command.ExecuteNonQuery();

                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error al crear tablas: {ex.Message}");
            }
        }

        // ✅ NUEVO: Migra la tabla existente si no tiene la columna NombreAlimento
        private void MigrarTablaConsumoSiEsNecesario(SQLiteConnection connection)
        {
            try
            {
                // Verificar si la columna NombreAlimento ya existe
                string checkColumn = "PRAGMA table_info(Consumo)";
                bool tieneNombreAlimento = false;
                bool tieneTabla = false;

                using (var cmd = new SQLiteCommand(checkColumn, connection))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        tieneTabla = true;
                        if (reader["name"]?.ToString() == "NombreAlimento")
                        {
                            tieneNombreAlimento = true;
                            break;
                        }
                    }
                }

                // Si la tabla existe pero no tiene NombreAlimento, agregarla
                if (tieneTabla && !tieneNombreAlimento)
                {
                    string addColumn = "ALTER TABLE Consumo ADD COLUMN NombreAlimento VARCHAR(200)";
                    using (var cmd = new SQLiteCommand(addColumn, connection))
                        cmd.ExecuteNonQuery();
                }
            }
            catch
            {
                // Si falla la migración, no interrumpir el flujo
            }
        }

        

        public bool ExisteRegistroSemana(int idLote, int numeroSemana, int año)
        {
            using (var connection = new SQLiteConnection(connectionString))
            {
                connection.Open();
                string query = @"
                    SELECT COUNT(*)   
                    FROM Consumo 
                    WHERE IdLoteGallinas = @IdLote 
                    AND NumeroSemana = @NumeroSemana 
                    AND Año = @Año";

                using (var command = new SQLiteCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@IdLote", idLote);
                    command.Parameters.AddWithValue("@NumeroSemana", numeroSemana);
                    command.Parameters.AddWithValue("@Año", año);
                    return Convert.ToInt64(command.ExecuteScalar()) > 0;
                }
            }
        }

        public (decimal consumoEsperado, decimal merma, bool alertaMerma) CalcularConsumo(
            int cantidadGallinas, decimal cantidadConsumida)
        {
            decimal racionSemanalPorGallina = 0.6m;
            decimal consumoEsperado = cantidadGallinas * racionSemanalPorGallina;
            decimal merma = consumoEsperado - cantidadConsumida;
            decimal mermaMaximaPermitida = cantidadGallinas * 0.1m;
            bool alertaMerma = merma > mermaMaximaPermitida;

            return (consumoEsperado, merma, alertaMerma);
        }

        // ✅ OPCIÓN B: InsertarConsumoSemanal guarda NombreAlimento directamente
        public bool InsertarConsumoSemanal(Consumo consumo)
        {
            try
            {
                var calendar = CultureInfo.CurrentCulture.Calendar;
                int numeroSemana = calendar.GetWeekOfYear(
                    consumo.FechaConsumo,
                    CalendarWeekRule.FirstDay,
                    DayOfWeek.Monday);
                int año = consumo.FechaConsumo.Year;

                if (ExisteRegistroSemana(consumo.IdLoteGallinas, numeroSemana, año))
                {
                    System.Windows.MessageBox.Show(
                        $"Ya existe un registro para el lote {consumo.IdLoteGallinas} " +
                        $"en la semana {numeroSemana} del {año}.\n\nSolo se permite un registro por semana.",
                        "Registro Duplicado",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Warning);
                    return false;
                }

                // ✅ OPCIÓN B: Obtener NombreAlimento desde Inventario si no viene asignado
                if (string.IsNullOrEmpty(consumo.NombreAlimento))
                {
                    var inventarioDB = new InventarioDatabase();
                    var item = inventarioDB.ObtenerTodosItems()
                               .Find(i => i.IdItem == consumo.IdAlimento);
                    consumo.NombreAlimento = item?.Nombre ?? "Alimento desconocido";
                }

                using (var connection = new SQLiteConnection(connectionString))
                {
                    connection.Open();

                    // ✅ OPCIÓN B: Se incluye NombreAlimento en el INSERT
                    string query = @"
                        INSERT INTO Consumo 
                        (FechaConsumo, IdLoteGallinas, IdAlimento, NombreAlimento,
                         CantidadConsumida, UnidadMedida, Turno, Observaciones, 
                         NumeroSemana, Año, CantidadGallinas, ConsumoEsperado, 
                         Merma, AlertaMerma)
                        VALUES 
                        (@FechaConsumo, @IdLoteGallinas, @IdAlimento, @NombreAlimento,
                         @CantidadConsumida, @UnidadMedida, @Turno, @Observaciones, 
                         @NumeroSemana, @Año, @CantidadGallinas, @ConsumoEsperado, 
                         @Merma, @AlertaMerma)";

                    using (var command = new SQLiteCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@FechaConsumo", consumo.FechaConsumo);
                        command.Parameters.AddWithValue("@IdLoteGallinas", consumo.IdLoteGallinas);
                        command.Parameters.AddWithValue("@IdAlimento", consumo.IdAlimento);
                        command.Parameters.AddWithValue("@NombreAlimento", consumo.NombreAlimento);
                        command.Parameters.AddWithValue("@CantidadConsumida", consumo.CantidadConsumida);
                        command.Parameters.AddWithValue("@UnidadMedida", consumo.UnidadMedida ?? "kg");
                        command.Parameters.AddWithValue("@Turno", consumo.Turno ?? "Semanal");
                        command.Parameters.AddWithValue("@Observaciones", consumo.Observaciones ?? string.Empty);
                        command.Parameters.AddWithValue("@NumeroSemana", numeroSemana);
                        command.Parameters.AddWithValue("@Año", año);
                        command.Parameters.AddWithValue("@CantidadGallinas", consumo.CantidadGallinas);
                        command.Parameters.AddWithValue("@ConsumoEsperado", consumo.ConsumoEsperado);
                        command.Parameters.AddWithValue("@Merma", consumo.Merma);
                        command.Parameters.AddWithValue("@AlertaMerma", consumo.AlertaMerma);

                        command.ExecuteNonQuery();
                    }

                    if (consumo.AlertaMerma)
                    {
                        System.Windows.MessageBox.Show(
                            $"⚠️ ALERTA DE MERMA REGISTRADA\n\n" +
                            $"Consumo esperado: {consumo.ConsumoEsperado:F2} kg\n" +
                            $"Consumo registrado: {consumo.CantidadConsumida:F2} kg\n" +
                            $"Merma: {consumo.Merma:F2} kg " +
                            $"({(consumo.Merma / consumo.CantidadGallinas * 1000):F0}g por gallina)\n\n" +
                            $"Revisar: salud de gallinas, calidad del alimento, desperdicios.",
                            "Alerta de Merma",
                            System.Windows.MessageBoxButton.OK,
                            System.Windows.MessageBoxImage.Warning);
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error al guardar: {ex.Message}");
                return false;
            }
        }

        // ✅ ObtenerConsumos usa NombreAlimento guardado directamente (ya no hace JOIN)
        public List<Consumo> ObtenerConsumos()
        {
            var consumos = new List<Consumo>();

            using (var connection = new SQLiteConnection(connectionString))
            {
                connection.Open();

                // ✅ OPCIÓN B: LEFT JOIN opcional, prioriza NombreAlimento guardado
                string query = @"
                    SELECT 
                        c.IdConsumo,
                        c.FechaConsumo,
                        c.IdLoteGallinas,
                        c.IdAlimento,
                        c.CantidadConsumida,
                        c.UnidadMedida,
                        c.Turno,
                        c.Observaciones,
                        c.NumeroSemana,
                        c.Año,
                        c.CantidadGallinas,
                        c.ConsumoEsperado,
                        c.Merma,
                        c.AlertaMerma,
                        COALESCE(c.NombreAlimento, 
                                 IFNULL(a.Nombre, 'Sin nombre')) AS NombreAlimento
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
                            NombreAlimento = reader["NombreAlimento"]?.ToString() ?? string.Empty,
                            CantidadConsumida = Convert.ToDecimal(reader["CantidadConsumida"]),
                            UnidadMedida = reader["UnidadMedida"]?.ToString() ?? string.Empty,
                            Turno = reader["Turno"]?.ToString() ?? string.Empty,
                            Observaciones = reader["Observaciones"]?.ToString() ?? string.Empty,
                            NumeroSemana = reader["NumeroSemana"] != DBNull.Value ? Convert.ToInt32(reader["NumeroSemana"]) : 0,
                            Año = reader["Año"] != DBNull.Value ? Convert.ToInt32(reader["Año"]) : 0,
                            CantidadGallinas = reader["CantidadGallinas"] != DBNull.Value ? Convert.ToInt32(reader["CantidadGallinas"]) : 0,
                            ConsumoEsperado = reader["ConsumoEsperado"] != DBNull.Value ? Convert.ToDecimal(reader["ConsumoEsperado"]) : 0,
                            Merma = reader["Merma"] != DBNull.Value ? Convert.ToDecimal(reader["Merma"]) : 0,
                            AlertaMerma = reader["AlertaMerma"] != DBNull.Value && Convert.ToBoolean(reader["AlertaMerma"])
                        });
                    }
                }
            }

            return consumos;
        }

        // Se mantiene para compatibilidad con código existente
        public List<Alimento> ObtenerAlimentos()
        {
            var alimentos = new List<Alimento>();

            using (var connection = new SQLiteConnection(connectionString))
            {
                connection.Open();
                string query = "SELECT IdAlimento, Nombre, StockDisponible FROM Alimento";

                using (var command = new SQLiteCommand(query, connection))
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        alimentos.Add(new Alimento
                        {
                            IdAlimento = Convert.ToInt32(reader["IdAlimento"]),
                            Nombre = reader["Nombre"]?.ToString() ?? string.Empty,
                            StockDisponible =(int) Convert.ToDecimal(reader["StockDisponible"])
                        });
                    }
                }
            }

            return alimentos;
        }

        public List<LoteGallina> ObtenerLotesActivos()
        {
            var lotes = new List<LoteGallina>();

            using (var connection = new SQLiteConnection(connectionString))
            {
                connection.Open();
                string query = @"
                    SELECT IdLote, Raza, CantidadGallinas AS CantidadActual, 
                           FechaIncorporacion AS FechaIngreso 
                    FROM Lote 
                    WHERE CantidadGallinas > 0
                    ORDER BY IdLote";

                using (var command = new SQLiteCommand(query, connection))
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        lotes.Add(new LoteGallina
                        {
                            IdLote = Convert.ToInt32(reader["IdLote"]),
                            Raza = reader["Raza"]?.ToString() ?? string.Empty,
                            CantidadActual = Convert.ToInt32(reader["CantidadActual"]),
                            FechaIngreso = Convert.ToDateTime(reader["FechaIngreso"])
                        });
                    }
                }
            }

            return lotes;
        }

        public decimal ObtenerConsumoDia()
        {
            using (var connection = new SQLiteConnection(connectionString))
            {
                connection.Open();
                string query = @"SELECT COALESCE(SUM(CantidadConsumida), 0) 
                                 FROM Consumo 
                                 WHERE DATE(FechaConsumo) = DATE('now')";
                using (var command = new SQLiteCommand(query, connection))
                    return Convert.ToDecimal(command.ExecuteScalar() ?? 0);
            }
        }

        public decimal ObtenerConsumoSemanal()
        {
            using (var connection = new SQLiteConnection(connectionString))
            {
                connection.Open();
                string query = @"SELECT COALESCE(SUM(CantidadConsumida), 0) 
                                 FROM Consumo 
                                 WHERE FechaConsumo >= DATE('now', '-7 days')";
                using (var command = new SQLiteCommand(query, connection))
                    return Convert.ToDecimal(command.ExecuteScalar() ?? 0);
            }
        }

        public List<ConsumoGrafica> ObtenerDatosParaGrafica()
        {
            var lista = new List<ConsumoGrafica>();
            using (var conexion = new SQLiteConnection(connectionString))
            {
                conexion.Open();
                // Consultamos los últimos 7 días de consumo
                string consulta = @"SELECT FechaConsumo, SUM(CantidadConsumo) as Total 
                            FROM Consumo 
                            GROUP BY FechaConsumo 
                            ORDER BY FechaConsumo DESC LIMIT 7";

                using (var comando = new SQLiteCommand(consulta, conexion))
                using (var reader = comando.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        lista.Add(new ConsumoGrafica
                        {
                            Fecha = Convert.ToDateTime(reader["FechaConsumo"]).ToString("dd/MM"),
                            Cantidad = Convert.ToDouble(reader["Total"])
                        });
                    }
                }
            }
            return lista;
        }

        // Clase auxiliar para el mapeo
        public class ConsumoGrafica
        {
            public string Fecha { get; set; }
            public double Cantidad { get; set; }
        }

        public decimal ObtenerAlimentoDisponible()
        {
            using (var connection = new SQLiteConnection(connectionString))
            {
                connection.Open();
                string query = "SELECT COALESCE(SUM(StockDisponible), 0) FROM Alimento";
                using (var command = new SQLiteCommand(query, connection))
                    return Convert.ToDecimal(command.ExecuteScalar() ?? 0);
            }
        }

        [Obsolete("Use InsertarConsumoSemanal en su lugar")]
        public bool InsertarConsumo(Consumo consumo)
        {
            return InsertarConsumoSemanal(consumo);
        }
    }
}