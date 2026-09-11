using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Text;
using System.Web;

namespace DevsFingerPrint.Infrastructure.Data
{
    public class LocalDatabase
    {

        private static string dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "local_data.db");
        public static string ConnectionString => $"Data Source={dbPath};Version=3;";

        public static void Inicializar()
        {
            if (!File.Exists(dbPath))
            {
                SQLiteConnection.CreateFile(dbPath);
            }

            using (var conexion = new SQLiteConnection(ConnectionString))
            {
                conexion.Open();

                string scriptTablas = @"
                    CREATE TABLE IF NOT EXISTS Empleado (
                        Id INTEGER PRIMARY KEY,
                        EmpresaId INTEGER NOT NULL,
                        Legajo TEXT,
                        DNI TEXT NOT NULL,
                        CUIL TEXT,
                        Nombre TEXT NOT NULL,
                        Apellido TEXT NOT NULL,
                        Departamento TEXT,
                        Categoria TEXT,
                        Sucursal TEXT,
                        Horario TEXT,
                        Activo INTEGER NOT NULL DEFAULT 1
                    );

                    CREATE TABLE IF NOT EXISTS Huella (
                        Id INTEGER PRIMARY KEY,
                        EmpleadoId INTEGER NOT NULL,
                        IndiceDedo INTEGER NOT NULL DEFAULT 0,
                        TemplateBiometrico TEXT NOT NULL,
                        FechaRegistro TEXT NOT NULL,
                        FOREIGN KEY(EmpleadoId) REFERENCES Empleado(Id)
                    );

                    CREATE TABLE IF NOT EXISTS Fichada (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        EmpleadoId INTEGER NOT NULL,
                        FechaHora TEXT NOT NULL,
                        TipoRegistro TEXT NOT NULL,
                        Metodo TEXT NOT NULL DEFAULT 'Biometrico',
                        Sincronizado INTEGER NOT NULL DEFAULT 0,
                        FOREIGN KEY(EmpleadoId) REFERENCES Empleado(Id)
                    );

                    CREATE TABLE IF NOT EXISTS ConfiguracionLocal (
                        Id INTEGER PRIMARY KEY,
                        Nombre TEXT,
                        EmpresaId INTEGER,
                        SerialLector TEXT
                    );
                ";

                using (var comando = new SQLiteCommand(scriptTablas, conexion))
                {
                    comando.ExecuteNonQuery();
                }
            }
        }


        // Método para guardar o actualizar los datos de la sucursal (ej. al loguearse por primera vez)
        public static void GuardarSucursalLocal(int id, string nombre, int empresaId, string serialLector)
        {
            try
            {
                using (var conexion = new SQLiteConnection(ConnectionString))
                {
                    conexion.Open();
                    string query = @"INSERT OR REPLACE INTO ConfiguracionLocal (Id, Nombre, EmpresaId, SerialLector) 
                             VALUES (@Id, @Nombre, @EmpresaId, @SerialLector)";

                    using (var cmd = new SQLiteCommand(query, conexion))
                    {
                        cmd.Parameters.AddWithValue("@Id", id);
                        cmd.Parameters.AddWithValue("@Nombre", nombre ?? string.Empty);
                        cmd.Parameters.AddWithValue("@EmpresaId", empresaId);
                        cmd.Parameters.AddWithValue("@SerialLector", serialLector ?? string.Empty);
                        cmd.ExecuteNonQuery();
                    }
                }
                System.Diagnostics.Debug.WriteLine("[DB] Sucursal guardada localmente con éxito.");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DB ERROR] No se pudo guardar la sucursal local: {ex.Message}");
            }
        }

        // Método para leer el SerialLector guardado localmente cuando no hay internet
        public static string ObtenerSerialLectorLocal()
        {
            try
            {
                using (var conexion = new SQLiteConnection(ConnectionString))
                {
                    conexion.Open();
                    string query = "SELECT SerialLector FROM ConfiguracionLocal LIMIT 1";
                    using (var cmd = new SQLiteCommand(query, conexion))
                    {
                        var resultado = cmd.ExecuteScalar();
                        return resultado != null ? resultado.ToString() : string.Empty;
                    }
                }
            }
            catch
            {
                return string.Empty;
            }
        }







    }
}
