using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using DevsFingerPrint.Domain.Models;
using DevsFingerPrint.Infrastructure.Data;
using DevsFingerPrint.Infrastructure.Repositories;
using DevsFingerPrint.Infrastructure.Services;
using Xunit;

namespace DevsFingerPrint.Tests
{
    public class SincronizacionCatalogoTests : IDisposable
    {
        private readonly string _databasePath;
        private readonly FichadaRepository _repository;

        public SincronizacionCatalogoTests()
        {
            string directory = Path.Combine(Path.GetTempPath(), "DevsFingerPrint.Tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            _databasePath = Path.Combine(directory, "test.db");
            LocalDatabase.Inicializar(_databasePath);
            _repository = new FichadaRepository(LocalDatabase.CrearConnectionString(_databasePath, pooling: false));
        }

        [Fact]
        public void ReemplazoCorrecto_ActualizaSqliteYElReemplazoFallidoConservaElAnterior()
        {
            var original = Empleado(1, "ORIGINAL");
            _repository.SincronizarCatalogoEmpresa(new[] { original }, new[] { Huella(1, "plantilla-original") });

            var nuevo = Empleado(4, "NUEVO");
            _repository.SincronizarCatalogoEmpresa(new[] { nuevo }, new[] { Huella(4, "plantilla-nueva") });

            Assert.Equal("plantilla-nueva", _repository.ObtenerHuellasLocales().Single().TemplateBiometrico);
            Assert.Equal(4, _repository.ObtenerEmpleadosLocales().Single().Id);

            Assert.ThrowsAny<Exception>(() => _repository.SincronizarCatalogoEmpresa(
                new[] { Empleado(8, "ROTO"), Empleado(8, "ROTO") },
                new[] { Huella(8, "no-debe-guardarse") }));

            Assert.Equal("plantilla-nueva", _repository.ObtenerHuellasLocales().Single().TemplateBiometrico);
            Assert.Equal("NUEVO", _repository.ObtenerEmpleadosLocales().Single().Legajo);
        }

        [Fact]
        public void RespuestaValidaVacia_PuedeReemplazarPorCero()
        {
            _repository.SincronizarCatalogoEmpresa(new[] { Empleado(1, "PREVIO") }, new[] { Huella(1, "previa") });

            _repository.SincronizarCatalogoEmpresa(new List<Empleado>(), new List<Huella>());

            Assert.Empty(_repository.ObtenerEmpleadosLocales());
            Assert.Empty(_repository.ObtenerHuellasLocales());
        }

        [Theory]
        [InlineData(HttpStatusCode.Unauthorized, "http-401")]
        [InlineData(HttpStatusCode.Forbidden, "http-403")]
        [InlineData(HttpStatusCode.InternalServerError, "http-500")]
        public async Task ErrorHttp_NoSePresentaComoCatalogoVacio(HttpStatusCode status, string motivo)
        {
            string baseUrl = CrearBaseUrl();
            using (var listener = new HttpListener())
            {
                listener.Prefixes.Add(baseUrl + "/");
                listener.Start();
                var server = AtenderAsync(listener, status, "{}");
                var client = new ApiClient(baseUrl);
                Assert.True(client.IniciarSesionAgente("cliente", "secreto"));

                ResultadoLista<Empleado> empleados = client.ObtenerEmpleados();
                await server;

                Assert.False(empleados.Exitoso);
                Assert.Equal(motivo, empleados.Motivo);
                Assert.Empty(empleados.Elementos);
            }
        }

        [Fact]
        public async Task JsonInvalido_NoReemplazaElCatalogo()
        {
            string baseUrl = CrearBaseUrl();
            using (var listener = new HttpListener())
            {
                listener.Prefixes.Add(baseUrl + "/");
                listener.Start();
                var server = AtenderAsync(listener, HttpStatusCode.OK, "no-json");
                var client = new ApiClient(baseUrl);
                Assert.True(client.IniciarSesionAgente("cliente", "secreto"));

                ResultadoLista<Huella> huellas = client.ObtenerHuellas();
                await server;

                Assert.False(huellas.Exitoso);
                Assert.Equal("json", huellas.Motivo);
            }
        }

        [Fact]
        public async Task RespuestaValidaVacia_EsExitosa()
        {
            string baseUrl = CrearBaseUrl();
            using (var listener = new HttpListener())
            {
                listener.Prefixes.Add(baseUrl + "/");
                listener.Start();
                var server = AtenderAsync(listener, HttpStatusCode.OK, "[]");
                var client = new ApiClient(baseUrl);
                Assert.True(client.IniciarSesionAgente("cliente", "secreto"));

                ResultadoLista<Empleado> empleados = client.ObtenerEmpleados();
                await server;

                Assert.True(empleados.Exitoso);
                Assert.Empty(empleados.Elementos);
            }
        }

        public void Dispose()
        {
            System.Data.SQLite.SQLiteConnection.ClearAllPools();
            string directory = Path.GetDirectoryName(_databasePath);
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }

        private static Empleado Empleado(int id, string legajo)
        {
            return new Empleado
            {
                Id = id,
                EmpresaId = 1,
                Legajo = legajo,
                DNI = "D" + id,
                Nombre = "N",
                Apellido = "A",
                Activo = true
            };
        }

        private static Huella Huella(int empleadoId, string template)
        {
            return new Huella
            {
                Id = empleadoId,
                EmpleadoId = empleadoId,
                IndiceDedo = 1,
                TemplateBiometrico = template,
                FechaRegistro = new DateTime(2026, 10, 26)
            };
        }

        private static async Task AtenderAsync(HttpListener listener, HttpStatusCode catalogStatus, string catalogBody)
        {
            int catalogos = 0;
            int autenticaciones = 0;
            while (catalogos < 1)
            {
                Task<HttpListenerContext> espera = listener.GetContextAsync();
                if (catalogos > 0 && await Task.WhenAny(espera, Task.Delay(500)) != espera)
                {
                    break;
                }

                HttpListenerContext context = await espera;
                bool esLogin = context.Request.Url.AbsolutePath.IndexOf("auth", StringComparison.OrdinalIgnoreCase) >= 0;
                string response;
                int status;
                if (esLogin && autenticaciones == 0)
                {
                    autenticaciones++;
                    response = "{\"token\":\"test-token\"}";
                    status = 200;
                }
                else if (esLogin)
                {
                    response = "{}";
                    status = 401;
                }
                else
                {
                    catalogos++;
                    response = catalogBody;
                    status = (int)catalogStatus;
                }

                byte[] bytes = Encoding.UTF8.GetBytes(response);
                context.Response.StatusCode = status;
                context.Response.ContentType = "application/json";
                context.Response.ContentLength64 = bytes.Length;
                await context.Response.OutputStream.WriteAsync(bytes, 0, bytes.Length);
                context.Response.Close();
            }

            Task<HttpListenerContext> renovacion = listener.GetContextAsync();
            if (await Task.WhenAny(renovacion, Task.Delay(800)) == renovacion)
            {
                HttpListenerContext context = await renovacion;
                byte[] bytes = Encoding.UTF8.GetBytes("{}");
                context.Response.StatusCode = 401;
                context.Response.ContentType = "application/json";
                context.Response.ContentLength64 = bytes.Length;
                await context.Response.OutputStream.WriteAsync(bytes, 0, bytes.Length);
                context.Response.Close();
            }
        }

        private static string CrearBaseUrl()
        {
            var probe = new TcpListener(IPAddress.Loopback, 0);
            try
            {
                probe.Start();
                int port = ((IPEndPoint)probe.LocalEndpoint).Port;
                return "http://127.0.0.1:" + port;
            }
            finally
            {
                probe.Stop();
            }
        }
    }
}
