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
                string sql = "SELECT Id, EmpleadoId, NombreDedo, TemplateBiometrico FROM Huella";

                using (var cmd = new SQLiteCommand(sql, conexion))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        lista.Add(new Huella
                        {
                            Id = Convert.ToInt32(reader["Id"]),
                            EmpleadoId = Convert.ToInt32(reader["EmpleadoId"]),
                            NombreDedo = reader["NombreDedo"].ToString(),
                            TemplateBiometrico = reader["TemplateBiometrico"].ToString()
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
                        var cmd = new SQLiteCommand(@"INSERT INTO Huella (Id, EmpleadoId, NombreDedo, TemplateBiometrico) 
                                                      VALUES (@Id, @EmpleadoId, @NombreDedo, @TemplateBiometrico)", conexion, transaccion);
                        cmd.Parameters.AddWithValue("@Id", h.Id);
                        cmd.Parameters.AddWithValue("@EmpleadoId", h.EmpleadoId);
                        cmd.Parameters.AddWithValue("@NombreDedo", h.NombreDedo);
                        cmd.Parameters.AddWithValue("@TemplateBiometrico", h.TemplateBiometrico);
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

    }
}
