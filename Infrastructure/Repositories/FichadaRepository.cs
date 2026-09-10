using DevsFingerPrint.Domain.Interfaces;
using DevsFingerPrint.Domain.Models;
using DevsFingerPrint.Infrastructure.Data;
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Linq;
using System.Text;

namespace DevsFingerPrint.Infrastructure.Repositories
{
    public class FichadaRepository : IFichadaRepository
    {


        public void GuardarFichadaLocal(Fichada fichada)
        {
            using (var conexion = new SQLiteConnection(LocalDatabase.ConnectionString))
            {
                conexion.Open();
                string sql = @"INSERT INTO Fichada (EmpleadoId, FechaHora, TipoRegistro, Metodo, Sincronizado) 
                               VALUES (@EmpleadoId, @FechaHora, @TipoRegistro, @Metodo, @Sincronizado)";

                using (var cmd = new SQLiteCommand(sql, conexion))
                {
                    cmd.Parameters.AddWithValue("@EmpleadoId", fichada.EmpleadoId);
                    cmd.Parameters.AddWithValue("@FechaHora", fichada.FechaHora.ToString("yyyy-MM-dd HH:mm:ss"));
                    cmd.Parameters.AddWithValue("@TipoRegistro", fichada.TipoRegistro);
                    cmd.Parameters.AddWithValue("@Metodo", fichada.Metodo);
                    cmd.Parameters.AddWithValue("@Sincronizado", fichada.Sincronizado ? 1 : 0);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public IEnumerable<Huella> ObtenerHuellasLocales()
        {
            var lista = new List<Huella>();

            using (var conexion = new SQLiteConnection(LocalDatabase.ConnectionString))
            {
                conexion.Open();
                string sql = "SELECT Id, EmpleadoId, IndiceDedo, TemplateBiometrico, FechaRegistro FROM Huella";

                using (var cmd = new SQLiteCommand(sql, conexion))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        lista.Add(new Huella
                        {
                            Id = Convert.ToInt32(reader["Id"]),
                            EmpleadoId = Convert.ToInt32(reader["EmpleadoId"]),
                            IndiceDedo = Convert.ToInt32(reader["IndiceDedo"]),
                            TemplateBiometrico = reader["TemplateBiometrico"].ToString(),
                            FechaRegistro = DateTime.Parse(reader["FechaRegistro"].ToString())
                        });
                    }
                }
            }
            return lista;
        }

        public IEnumerable<Fichada> ObtenerFichadasPendientes()
        {
            var lista = new List<Fichada>();

            using (var conexion = new SQLiteConnection(LocalDatabase.ConnectionString))
            {
                conexion.Open();
                string sql = "SELECT Id, EmpleadoId, FechaHora, TipoRegistro, Metodo FROM Fichada WHERE Sincronizado = 0";

                using (var cmd = new SQLiteCommand(sql, conexion))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        lista.Add(new Fichada
                        {
                            Id = Convert.ToInt32(reader["Id"]),
                            EmpleadoId = Convert.ToInt32(reader["EmpleadoId"]),
                            FechaHora = DateTime.Parse(reader["FechaHora"].ToString()),
                            TipoRegistro = reader["TipoRegistro"].ToString(),
                            Metodo = reader["Metodo"].ToString(),
                            Sincronizado = false
                        });
                    }
                }
            }
            return lista;
        }

        public void MarcarComoSincronizadas(IEnumerable<int> idsFichadas)
        {
            using (var conexion = new SQLiteConnection(LocalDatabase.ConnectionString))
            {
                conexion.Open();
                using (var transaccion = conexion.BeginTransaction())
                {
                    string sql = "UPDATE Fichada SET Sincronizado = 1 WHERE Id = @Id";

                    foreach (var id in idsFichadas)
                    {
                        using (var cmd = new SQLiteCommand(sql, conexion, transaccion))
                        {
                            cmd.Parameters.AddWithValue("@Id", id);
                            cmd.ExecuteNonQuery();
                        }
                    }
                    transaccion.Commit();
                }
            }
        }

        public void SincronizarCatalogoEmpresa(IEnumerable<Empleado> empleados, IEnumerable<Huella> huellas)
        {
            using (var conexion = new SQLiteConnection(LocalDatabase.ConnectionString))
            {
                conexion.Open();
                using (var transaccion = conexion.BeginTransaction())
                {
                    // Limpiar catálogo previo e insertar el actualizado
                    new SQLiteCommand("DELETE FROM Huella;", conexion, transaccion).ExecuteNonQuery();
                    new SQLiteCommand("DELETE FROM Empleado;", conexion, transaccion).ExecuteNonQuery();

                    foreach (var emp in empleados)
                    {
                        var cmd = new SQLiteCommand(@"INSERT INTO Empleado (Id, EmpresaId, Legajo, DNI, CUIL, Nombre, Apellido, Departamento, Categoria, Sucursal, Horario, Activo) 
                                                      VALUES (@Id, @EmpresaId, @Legajo, @DNI, @CUIL, @Nombre, @Apellido, @Departamento, @Categoria, @Sucursal, @Horario, @Activo)", conexion, transaccion);
                        cmd.Parameters.AddWithValue("@Id", emp.Id);
                        cmd.Parameters.AddWithValue("@EmpresaId", emp.EmpresaId);
                        cmd.Parameters.AddWithValue("@Legajo", emp.Legajo ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@DNI", emp.DNI);
                        cmd.Parameters.AddWithValue("@CUIL", emp.CUIL ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@Nombre", emp.Nombre);
                        cmd.Parameters.AddWithValue("@Apellido", emp.Apellido);
                        cmd.Parameters.AddWithValue("@Departamento", emp.Departamento ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@Categoria", emp.Categoria ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@Sucursal", emp.Sucursal ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@Horario", emp.Horario ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@Activo", emp.Activo ? 1 : 0);
                        cmd.ExecuteNonQuery();
                    }

                    foreach (var h in huellas)
                    {
                        var cmd = new SQLiteCommand(@"INSERT INTO Huella (Id, EmpleadoId, IndiceDedo, TemplateBiometrico, FechaRegistro) 
                                                      VALUES (@Id, @EmpleadoId, @IndiceDedo, @TemplateBiometrico, @FechaRegistro)", conexion, transaccion);
                        cmd.Parameters.AddWithValue("@Id", h.Id);
                        cmd.Parameters.AddWithValue("@EmpleadoId", h.EmpleadoId);
                        cmd.Parameters.AddWithValue("@IndiceDedo", h.IndiceDedo);
                        cmd.Parameters.AddWithValue("@TemplateBiometrico", h.TemplateBiometrico);
                        cmd.Parameters.AddWithValue("@FechaRegistro", h.FechaRegistro);
                        cmd.ExecuteNonQuery();
                    }

                    transaccion.Commit();
                }
            }
        }

        public bool EnviarLoteFichadas(IEnumerable<Fichada> fichadas)
        {
            // Este método será invocado por el ApiClient cuando sincronice con el Backend MySQL
            throw new NotImplementedException();
        }

        public bool LimpiarFichadasSincronizadas()
        {
            try
            {
                using (var conexion = new SQLiteConnection(LocalDatabase.ConnectionString))
                {
                    conexion.Open();

                    string query = @"
                DELETE FROM Fichada 
                WHERE Sincronizado = 1 
                AND Id NOT IN (
                    SELECT MaxId FROM (
                        SELECT MAX(Id) AS MaxId 
                        FROM Fichada 
                        GROUP BY EmpleadoId
                    ) AS Sub
                );";

                    using (var command = new SQLiteCommand(query, conexion))
                    {
                        int filasAfectadas = command.ExecuteNonQuery();
                        System.Diagnostics.Debug.WriteLine("Limpieza local completada. Registros eliminados: " + filasAfectadas);
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Error al limpiar fichadas locales sincronizadas: " + ex.Message);
                return false;
            }
        }

        // Obtiene la última fichada registrada localmente para un empleado específico
        public Fichada ObtenerUltimaFichada(int empleadoId)
        {
            using (var conexion = new SQLiteConnection(LocalDatabase.ConnectionString))
            {
                conexion.Open();
                string query = @"SELECT Id, EmpleadoId, FechaHora, TipoRegistro, Metodo, Sincronizado 
                                 FROM Fichada
                                 WHERE EmpleadoId = @EmpleadoId 
                                 ORDER BY FechaHora DESC 
                                 LIMIT 1;";

                using (var command = new SQLiteCommand(query, conexion))
                {
                    command.Parameters.AddWithValue("@EmpleadoId", empleadoId);

                    using (var reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            return new Fichada
                            {
                                Id = reader.GetInt32(0),
                                EmpleadoId = reader.GetInt32(1),
                                FechaHora = Convert.ToDateTime(reader.GetString(2)), // O GetDateTime según el tipo de columna en SQLite
                                TipoRegistro = reader.GetString(3),
                                Metodo = reader.GetString(4),
                                Sincronizado = reader.GetInt32(5) == 1
                            };
                        }
                    }
                }
            }
            return null;
        }

        // Obtiene el horario laboral teórico del empleado (configurado en base local)
        public HorarioLaboral ObtenerHorarioLaboral(int empleadoId)
        {
            using (var conexion = new SQLiteConnection(LocalDatabase.ConnectionString))
            {
                conexion.Open();
                // Ajustá el nombre de la columna y tabla según tu esquema actual (ej: Empleados, Horario)
                string query = @"SELECT Horario FROM Empleado WHERE Id = @EmpleadoId;";

                using (var command = new SQLiteCommand(query, conexion))
                {
                    command.Parameters.AddWithValue("@EmpleadoId", empleadoId);

                    using (var reader = command.ExecuteReader())
                    {
                        if (reader.Read() && !reader.IsDBNull(0))
                        {
                            string horarioTexto = reader.GetString(0); // Ej: "09:00 a 18:00"

                            // Separamos el string usando " a " como delimitador
                            string[] partes = horarioTexto.Split(new[] { " a " }, StringSplitOptions.RemoveEmptyEntries);

                            if (partes.Length == 2)
                            {
                                return new HorarioLaboral
                                {
                                    HoraEntrada = TimeSpan.Parse(partes[0].Trim()),
                                    HoraSalida = TimeSpan.Parse(partes[1].Trim())
                                };
                            }
                        }
                    }
                }
            }
            return null;
        }

    }
}


// Modelo complementario si no lo tienes definido
public class HorarioLaboral
{
    public TimeSpan HoraEntrada { get; set; }
    public TimeSpan HoraSalida { get; set; }
}

