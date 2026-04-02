using System;
using System.Data;
using System.Data.SQLite;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace loginavícola.Model
{
    public class DatabaseModel
    {
        private string connectionString;

        public DatabaseModel()
        {
            // Ruta de la base de datos (puedes dejar el nombre del archivo con o sin tilde, 
            // pero el namespace debe ser exacto)
            string databasePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "sistema_avicola.db");
            connectionString = $"Data Source={databasePath};Version=3;";
            InicializarBaseDeDatos();
        }

        public string EncriptarSHA256(string texto)
        {
            using (SHA256 sha256Hash = SHA256.Create())
            {
                byte[] bytes = sha256Hash.ComputeHash(Encoding.UTF8.GetBytes(texto));
                StringBuilder builder = new StringBuilder();
                for (int i = 0; i < bytes.Length; i++)
                    builder.Append(bytes[i].ToString("x2"));
                return builder.ToString();
            }
        }

        public SQLiteConnection GetConnection() => new SQLiteConnection(connectionString);

        public void InicializarBaseDeDatos()
        {
            try
            {
                using (var conn = GetConnection())
                {
                    conn.Open();
                    string query = @"CREATE TABLE IF NOT EXISTS usuarios (
                                        id INTEGER PRIMARY KEY AUTOINCREMENT,
                                        username TEXT NOT NULL UNIQUE,
                                        password TEXT NOT NULL,
                                        nombre TEXT NOT NULL,
                                        rol TEXT NOT NULL)";
                    using (var cmd = new SQLiteCommand(query, conn)) { cmd.ExecuteNonQuery(); }

                    // Admin por defecto
                    string checkAdmin = "SELECT COUNT(*) FROM usuarios";
                    using (var cmdCheck = new SQLiteCommand(checkAdmin, conn))
                    {
                        if (Convert.ToInt32(cmdCheck.ExecuteScalar()) == 0)
                        {
                            string pass = EncriptarSHA256("admin123");
                            string insert = "INSERT INTO usuarios (username, password, nombre, rol) VALUES ('admin', @p, 'Admin', 'admin')";
                            using (var cmdInsert = new SQLiteCommand(insert, conn))
                            {
                                cmdInsert.Parameters.AddWithValue("@p", pass);
                                cmdInsert.ExecuteNonQuery();
                            }
                        }
                    }
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine("Error: " + ex.Message); }
        }

        public int ExecuteNonQuery(string query, SQLiteParameter[] parameters = null)
        {
            using (var conn = GetConnection())
            {
                conn.Open();
                using (var cmd = new SQLiteCommand(query, conn))
                {
                    if (parameters != null) cmd.Parameters.AddRange(parameters);
                    return cmd.ExecuteNonQuery();
                }
            }
        }

        public DataTable ExecuteQuery(string query, SQLiteParameter[] parameters = null)
        {
            DataTable dt = new DataTable();
            using (var conn = GetConnection())
            {
                conn.Open();
                using (var cmd = new SQLiteCommand(query, conn))
                {
                    if (parameters != null) cmd.Parameters.AddRange(parameters);
                    using (var adapter = new SQLiteDataAdapter(cmd)) { adapter.Fill(dt); }
                }
            }
            return dt;
        }
    }
}