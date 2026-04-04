using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using loginavicola.Model;

namespace loginavicola.Database
{
    public class UsuarioDatabase
    {
        private readonly string connectionString;
        private readonly string dbPath;

        public UsuarioDatabase()
        {
            // Ruta automática para evitar pérdidas de archivo
            dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "sistema_avicola.db");
            connectionString = $"Data Source={dbPath};Version=3;";

            CrearBaseDeDatos();
            CrearTablaUsuarios();
        }

        private void CrearBaseDeDatos()
        {
            try
            {
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

        private void CrearTablaUsuarios()
        {
            try
            {
                using (var connection = new SQLiteConnection(connectionString))
                {
                    connection.Open();

                    string createTable = @"
                        CREATE TABLE IF NOT EXISTS Usuario (
                            IdUsuario INTEGER PRIMARY KEY AUTOINCREMENT,
                            Nombres VARCHAR(100) NOT NULL,
                            Apellidos VARCHAR(100) NOT NULL,
                            Username VARCHAR(50) UNIQUE NOT NULL,
                            Documento VARCHAR(20) UNIQUE NOT NULL,
                            Telefono VARCHAR(20),
                            Direccion VARCHAR(200),
                            Email VARCHAR(100) UNIQUE NOT NULL,
                            Password VARCHAR(255) NOT NULL,
                            Rol VARCHAR(50) DEFAULT 'Usuario',
                            FechaCreacion DATE DEFAULT CURRENT_TIMESTAMP,
                            Activo BOOLEAN DEFAULT 1,
                            PermisoInicio BOOLEAN DEFAULT 1,
                            PermisoLotes BOOLEAN DEFAULT 0,
                            PermisoProduccion BOOLEAN DEFAULT 0,
                            PermisoAlimentacion BOOLEAN DEFAULT 0,
                            PermisoEntregas BOOLEAN DEFAULT 0,
                            PermisoDiagnostico BOOLEAN DEFAULT 0,
                            PermisoInventario BOOLEAN DEFAULT 0,
                            PermisoGestionUsuarios BOOLEAN DEFAULT 0
                        )";

                    using (var command = new SQLiteCommand(createTable, connection))
                    {
                        command.ExecuteNonQuery();
                    }

                    CrearAdministradorPorDefecto(connection);
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error al crear tabla: {ex.Message}");
            }
        }

        private void CrearAdministradorPorDefecto(SQLiteConnection connection)
        {
            try
            {
                string checkQuery = "SELECT COUNT(*) FROM Usuario WHERE Email = 'admin@avicola.com' OR Username = 'admin'";
                using (var command = new SQLiteCommand(checkQuery, connection))
                {
                    long count = (long)command.ExecuteScalar();

                    if (count == 0)
                    {
                        // Contraseña por defecto: admin123
                        string passwordHash = HashPassword("admin123");

                        string insertQuery = @"
                            INSERT INTO Usuario 
                            (Nombres, Apellidos, Username, Documento, Telefono, Direccion, Email, Password, Rol, 
                             PermisoInicio, PermisoLotes, PermisoProduccion, PermisoAlimentacion, 
                             PermisoEntregas, PermisoDiagnostico, PermisoInventario, PermisoGestionUsuarios)
                            VALUES 
                            ('Administrador', 'Sistema', 'admin', '00000000', '000-0000', 'Oficina Principal', 
                             'admin@avicola.com', @Password, 'Administrador', 1, 1, 1, 1, 1, 1, 1, 1)";

                        using (var insertCommand = new SQLiteCommand(insertQuery, connection))
                        {
                            insertCommand.Parameters.AddWithValue("@Password", passwordHash);
                            insertCommand.ExecuteNonQuery();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al crear administrador: {ex.Message}");
            }
        }

        private string HashPassword(string password)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
                StringBuilder builder = new StringBuilder();
                foreach (byte b in bytes)
                {
                    builder.Append(b.ToString("x2"));
                }
                return builder.ToString();
            }
        }

        public Usuario ValidarLogin(string username, string password)
        {
            try
            {
                string passwordHash = HashPassword(password);
                using (var connection = new SQLiteConnection(connectionString))
                {
                    connection.Open();
                    // Busca por Username o Email para que sea más flexible
                    string query = "SELECT * FROM Usuario WHERE (Username = @User OR Email = @User) AND Password = @Pass AND Activo = 1";

                    using (var command = new SQLiteCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@User", username);
                        command.Parameters.AddWithValue("@Pass", passwordHash);

                        using (var reader = command.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                return MapearUsuario(reader);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error al validar login: {ex.Message}");
            }
            return null;
        }

        public bool InsertarUsuario(Usuario usuario, string password)
        {
            try
            {
                using (var connection = new SQLiteConnection(connectionString))
                {
                    connection.Open();
                    string passwordHash = HashPassword(password);

                    // Si el Username está vacío, usa el Email
                    string usernameReal = string.IsNullOrEmpty(usuario.Username) ? usuario.Email : usuario.Username;

                    string query = @"
                        INSERT INTO Usuario 
                        (Nombres, Apellidos, Username, Documento, Telefono, Direccion, Email, Password, Rol,
                         PermisoInicio, PermisoLotes, PermisoProduccion, PermisoAlimentacion,
                         PermisoEntregas, PermisoDiagnostico, PermisoInventario, PermisoGestionUsuarios)
                        VALUES 
                        (@Nombres, @Apellidos, @Username, @Documento, @Telefono, @Direccion, @Email, @Password, @Rol,
                         @PermisoInicio, @PermisoLotes, @PermisoProduccion, @PermisoAlimentacion,
                         @PermisoEntregas, @PermisoDiagnostico, @PermisoInventario, @PermisoGestionUsuarios)";

                    using (var command = new SQLiteCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@Nombres", usuario.Nombres);
                        command.Parameters.AddWithValue("@Apellidos", usuario.Apellidos);
                        command.Parameters.AddWithValue("@Username", usernameReal);
                        command.Parameters.AddWithValue("@Documento", usuario.Documento);
                        command.Parameters.AddWithValue("@Telefono", usuario.Telefono ?? string.Empty);
                        command.Parameters.AddWithValue("@Direccion", usuario.Direccion ?? string.Empty);
                        command.Parameters.AddWithValue("@Email", usuario.Email);
                        command.Parameters.AddWithValue("@Password", passwordHash);
                        command.Parameters.AddWithValue("@Rol", usuario.Rol);
                        command.Parameters.AddWithValue("@PermisoInicio", usuario.PermisoInicio);
                        command.Parameters.AddWithValue("@PermisoLotes", usuario.PermisoLotes);
                        command.Parameters.AddWithValue("@PermisoProduccion", usuario.PermisoProduccion);
                        command.Parameters.AddWithValue("@PermisoAlimentacion", usuario.PermisoAlimentacion);
                        command.Parameters.AddWithValue("@PermisoEntregas", usuario.PermisoEntregas);
                        command.Parameters.AddWithValue("@PermisoDiagnostico", usuario.PermisoDiagnostico);
                        command.Parameters.AddWithValue("@PermisoInventario", usuario.PermisoInventario);
                        command.Parameters.AddWithValue("@PermisoGestionUsuarios", usuario.PermisoGestionUsuarios);

                        command.ExecuteNonQuery();
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error al guardar usuario: {ex.Message}");
                return false;
            }
        }

        public bool ActualizarPermisos(Usuario usuario)
        {
            try
            {
                using (var connection = new SQLiteConnection(connectionString))
                {
                    connection.Open();
                    string query = @"
                        UPDATE Usuario SET 
                            PermisoInicio = @PermisoInicio,
                            PermisoLotes = @PermisoLotes,
                            PermisoProduccion = @PermisoProduccion,
                            PermisoAlimentacion = @PermisoAlimentacion,
                            PermisoEntregas = @PermisoEntregas,
                            PermisoDiagnostico = @PermisoDiagnostico,
                            PermisoInventario = @PermisoInventario,
                            PermisoGestionUsuarios = @PermisoGestionUsuarios
                        WHERE IdUsuario = @IdUsuario";

                    using (var command = new SQLiteCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@IdUsuario", usuario.IdUsuario);
                        command.Parameters.AddWithValue("@PermisoInicio", usuario.PermisoInicio);
                        command.Parameters.AddWithValue("@PermisoLotes", usuario.PermisoLotes);
                        command.Parameters.AddWithValue("@PermisoProduccion", usuario.PermisoProduccion);
                        command.Parameters.AddWithValue("@PermisoAlimentacion", usuario.PermisoAlimentacion);
                        command.Parameters.AddWithValue("@PermisoEntregas", usuario.PermisoEntregas);
                        command.Parameters.AddWithValue("@PermisoDiagnostico", usuario.PermisoDiagnostico);
                        command.Parameters.AddWithValue("@PermisoInventario", usuario.PermisoInventario);
                        command.Parameters.AddWithValue("@PermisoGestionUsuarios", usuario.PermisoGestionUsuarios);

                        command.ExecuteNonQuery();
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error al actualizar permisos: {ex.Message}");
                return false;
            }
        }

        public List<Usuario> ObtenerTodosLosUsuarios()
        {
            var usuarios = new List<Usuario>();
            try
            {
                using (var connection = new SQLiteConnection(connectionString))
                {
                    connection.Open();
                    string query = "SELECT * FROM Usuario WHERE Activo = 1 ORDER BY FechaCreacion DESC";

                    using (var command = new SQLiteCommand(query, connection))
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            usuarios.Add(MapearUsuario(reader));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error al obtener usuarios: {ex.Message}");
            }
            return usuarios;
        }

        private Usuario MapearUsuario(SQLiteDataReader reader)
        {
            return new Usuario
            {
                IdUsuario = Convert.ToInt32(reader["IdUsuario"]),
                Nombres = reader["Nombres"]?.ToString() ?? string.Empty,
                Apellidos = reader["Apellidos"]?.ToString() ?? string.Empty,
                Username = reader["Username"]?.ToString() ?? string.Empty,
                Documento = reader["Documento"]?.ToString() ?? string.Empty,
                Telefono = reader["Telefono"]?.ToString() ?? string.Empty,
                Direccion = reader["Direccion"]?.ToString() ?? string.Empty,
                Email = reader["Email"]?.ToString() ?? string.Empty,
                Rol = reader["Rol"]?.ToString() ?? string.Empty,
                PermisoInicio = Convert.ToBoolean(reader["PermisoInicio"]),
                PermisoLotes = Convert.ToBoolean(reader["PermisoLotes"]),
                PermisoProduccion = Convert.ToBoolean(reader["PermisoProduccion"]),
                PermisoAlimentacion = Convert.ToBoolean(reader["PermisoAlimentacion"]),
                PermisoEntregas = Convert.ToBoolean(reader["PermisoEntregas"]),
                PermisoDiagnostico = Convert.ToBoolean(reader["PermisoDiagnostico"]),
                PermisoInventario = Convert.ToBoolean(reader["PermisoInventario"]),
                PermisoGestionUsuarios = Convert.ToBoolean(reader["PermisoGestionUsuarios"])
            };
        }
    }
}