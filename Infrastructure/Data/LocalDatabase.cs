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
                        NombreDedo TEXT NOT NULL,
                        TemplateBiometrico TEXT NOT NULL,
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
                ";

                using (var comando = new SQLiteCommand(scriptTablas, conexion))
                {
                    comando.ExecuteNonQuery();
                }
            }
        }










    }
}
