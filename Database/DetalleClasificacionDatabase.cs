using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data.SQLite;
using System.IO;

namespace loginavicola.Database
{
    public class DetalleClasificacionDatabase
    {
        private readonly string connectionString;

        public DetalleClasificacionDatabase()
        {
            DatabaseHelper.Inicializar();
            connectionString = DatabaseHelper.ConnectionString;

            CrearTabla();
        }

        private void CrearTabla()
        {
            using (var connection = new SQLiteConnection(connectionString))
            {
                connection.Open();

                string sql = @"
                CREATE TABLE IF NOT EXISTS DetalleClasificacion
                (
                    IdDetalle INTEGER PRIMARY KEY AUTOINCREMENT,

                    IdClasificacion INTEGER NOT NULL,

                    Peso REAL NOT NULL,

                    Volumen REAL,

                    Categoria TEXT NOT NULL,

                    FechaHora TEXT,

                    Origen TEXT,

                    FOREIGN KEY(IdClasificacion)
                    REFERENCES ClasificacionProduccion(IdClasificacion)
                );";

                using (var cmd = new SQLiteCommand(sql, connection))
                {
                    cmd.ExecuteNonQuery();
                }
            }
        }
    }
}