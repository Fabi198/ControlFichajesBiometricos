using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using DevsFingerPrint.Domain.Models;
using DevsFingerPrint.Infrastructure.Services;
using Newtonsoft.Json.Linq;
using Xunit;

namespace DevsFingerPrint.Tests
{
    public class ApiClientTests
    {
        [Fact]
        public async Task EnviarFichadas_EnvioBiometricoUsaPayloadTimestampOnlyYConfirmaCantidad()
        {
            string[] requestBodies = new string[2];
            string[] authorizationHeaders = new string[2];
            string baseUrl = CrearBaseUrl();
            using (var listener = new HttpListener())
            {
                listener.Prefixes.Add(baseUrl + "/");
                listener.Start();
                var server = AtenderLoginYFichadasAsync(listener, requestBodies, authorizationHeaders, "{\"recibidas\":2}");
                var client = new ApiClient(baseUrl);

                Assert.True(client.IniciarSesionAgente("test\"-client", "test\\-secret"));
                var fichadas = new[]
                {
                    CrearFichada(7, new DateTime(2026, 10, 12, 9, 0, 0)),
                    CrearFichada(7, new DateTime(2026, 10, 12, 18, 0, 0))
                };

                Assert.True(client.EnviarFichadas(fichadas.ToList()));
                await server;

                var login = JObject.Parse(requestBodies[0]);
                Assert.Equal("test\"-client", login["clientId"].Value<string>());
                Assert.Equal("test\\-secret", login["clientSecret"].Value<string>());
                var payload = JArray.Parse(requestBodies[1]);
                Assert.Equal(2, payload.Count);
                Assert.All(payload, item =>
                {
                    Assert.NotNull(item["empleadoId"]);
                    Assert.NotNull(item["fechaHora"]);
                    Assert.Null(item["tipoRegistro"]);
                    Assert.Null(item["metodo"]);
                });
                Assert.Equal(
                    payload.Select(item => item["fechaHora"].Value<DateTime>()).OrderBy(time => time),
                    payload.Select(item => item["fechaHora"].Value<DateTime>()));
                Assert.Equal("Bearer test-token", authorizationHeaders[1]);
            }
        }

        [Theory]
        [InlineData("{\"recibidas\":1}", true)]
        [InlineData("{\"recibidas\":0}", false)]
        [InlineData("{}", false)]
        [InlineData("not-json", false)]
        public async Task EnviarFichadas_SoloAceptaConfirmacionExacta(string serverResponse, bool expected)
        {
            var bodies = new string[2];
            var headers = new string[2];
            var baseUrl = CrearBaseUrl();
            using (var listener = new HttpListener())
            {
                listener.Prefixes.Add(baseUrl + "/");
                listener.Start();
                var server = AtenderLoginYFichadasAsync(listener, bodies, headers, serverResponse);
                var client = new ApiClient(baseUrl);
                Assert.True(client.IniciarSesionAgente("test-client", "test-secret"));

                Assert.Equal(expected, client.EnviarFichadas(new[]
                {
                    CrearFichada(9, new DateTime(2026, 10, 13, 9, 0, 0))
                }.ToList()));

                await server;
            }
        }

        [Fact]
        public async Task EnviarFichadas_AceptaLoteMaximoDe500ConConfirmacionCompleta()
        {
            var bodies = new string[2];
            var headers = new string[2];
            var baseUrl = CrearBaseUrl();
            using (var listener = new HttpListener())
            {
                listener.Prefixes.Add(baseUrl + "/");
                listener.Start();
                var server = AtenderLoginYFichadasAsync(listener, bodies, headers, "{\"recibidas\":500}");
                var client = new ApiClient(baseUrl);
                Assert.True(client.IniciarSesionAgente("test-client", "test-secret"));
                var events = Enumerable.Range(0, 500)
                    .Select(index => CrearFichada(3, new DateTime(2026, 10, 13).AddMinutes(index)))
                    .ToList();

                Assert.True(client.EnviarFichadas(events));
                await server;
                Assert.Equal(500, JArray.Parse(bodies[1]).Count);
            }
        }

        [Fact]
        public void EnviarFichadas_RechazaLoteVacioSinIntentarAutenticar()
        {
            var client = new ApiClient("http://127.0.0.1");

            Assert.False(client.EnviarFichadas(new System.Collections.Generic.List<Fichada>()));
        }

        [Fact]
        public async Task EnviarFichadas_ErrorHttpConservaFalloDeConfirmacion()
        {
            var bodies = new string[2];
            var headers = new string[2];
            var baseUrl = CrearBaseUrl();
            using (var listener = new HttpListener())
            {
                listener.Prefixes.Add(baseUrl + "/");
                listener.Start();
                var server = AtenderLoginYFichadasAsync(listener, bodies, headers, "{\"error\":\"temporary\"}", HttpStatusCode.InternalServerError);
                var client = new ApiClient(baseUrl);
                Assert.True(client.IniciarSesionAgente("test-client", "test-secret"));

                Assert.False(client.EnviarFichadas(new[]
                {
                    CrearFichada(9, new DateTime(2026, 10, 13, 9, 0, 0))
                }.ToList()));
                await server;
            }
        }

        [Fact]
        public async Task EnviarFichadas_RenuevaSesionEn401YReintentaElMismoLote()
        {
            var bodies = new string[4];
            var headers = new string[4];
            var baseUrl = CrearBaseUrl();
            using (var listener = new HttpListener())
            {
                listener.Prefixes.Add(baseUrl + "/");
                listener.Start();
                var server = AtenderRenovacionAsync(listener, bodies, headers);
                ApiClient client = null;
                client = new ApiClient(baseUrl, () => client.IniciarSesionAgente("renew-client", "renew-secret"));
                Assert.True(client.IniciarSesionAgente("initial-client", "initial-secret"));

                Assert.True(client.EnviarFichadas(new[]
                {
                    CrearFichada(15, new DateTime(2026, 10, 13, 9, 0, 0))
                }.ToList()));
                await server;

                Assert.Equal("Bearer initial-token", headers[1]);
                Assert.Equal("Bearer renewed-token", headers[3]);
                Assert.Equal(bodies[1], bodies[3]);
                Assert.Equal("renew-client", JObject.Parse(bodies[2])["clientId"].Value<string>());
            }
        }

        [Fact]
        public void EnviarFichadas_RechazaLoteDe501AntesDeEnviarEventos()
        {
            var client = new ApiClient("http://127.0.0.1");
            var fichadas = Enumerable.Range(0, 501)
                .Select(index => CrearFichada(9, new DateTime(2026, 10, 13).AddMinutes(index)))
                .ToList();

            Assert.False(client.EnviarFichadas(fichadas));
        }

        private static async Task AtenderLoginYFichadasAsync(
            HttpListener listener,
            string[] requestBodies,
            string[] authorizationHeaders,
            string fichadasResponse,
            HttpStatusCode fichadasStatus = HttpStatusCode.OK)
        {
            for (int requestIndex = 0; requestIndex < 2; requestIndex++)
            {
                var context = await listener.GetContextAsync();
                using (var reader = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding))
                {
                    requestBodies[requestIndex] = await reader.ReadToEndAsync();
                }

                authorizationHeaders[requestIndex] = context.Request.Headers["Authorization"];
                string response = requestIndex == 0 ? "{\"token\":\"test-token\"}" : fichadasResponse;
                byte[] responseBytes = Encoding.UTF8.GetBytes(response);
                context.Response.StatusCode = requestIndex == 0 ? (int)HttpStatusCode.OK : (int)fichadasStatus;
                context.Response.ContentType = "application/json";
                context.Response.ContentLength64 = responseBytes.Length;
                await context.Response.OutputStream.WriteAsync(responseBytes, 0, responseBytes.Length);
                context.Response.Close();
            }
        }

        private static async Task AtenderRenovacionAsync(
            HttpListener listener,
            string[] requestBodies,
            string[] authorizationHeaders)
        {
            for (int requestIndex = 0; requestIndex < 4; requestIndex++)
            {
                var context = await listener.GetContextAsync();
                using (var reader = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding))
                {
                    requestBodies[requestIndex] = await reader.ReadToEndAsync();
                }

                authorizationHeaders[requestIndex] = context.Request.Headers["Authorization"];
                string response;
                if (requestIndex == 0)
                {
                    response = "{\"token\":\"initial-token\"}";
                }
                else if (requestIndex == 1)
                {
                    context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
                    response = "{\"mensaje\":\"expired\"}";
                }
                else if (requestIndex == 2)
                {
                    response = "{\"token\":\"renewed-token\"}";
                }
                else
                {
                    response = "{\"recibidas\":1}";
                }

                byte[] responseBytes = Encoding.UTF8.GetBytes(response);
                context.Response.ContentType = "application/json";
                context.Response.ContentLength64 = responseBytes.Length;
                await context.Response.OutputStream.WriteAsync(responseBytes, 0, responseBytes.Length);
                context.Response.Close();
            }
        }

        private static string CrearBaseUrl()
        {
            var portProbe = new TcpListener(IPAddress.Loopback, 0);
            try
            {
                portProbe.Start();
                int port = ((IPEndPoint)portProbe.LocalEndpoint).Port;
                return $"http://127.0.0.1:{port}";
            }
            finally
            {
                portProbe.Stop();
            }
        }

        private static Fichada CrearFichada(int empleadoId, DateTime fechaHora)
        {
            return new Fichada
            {
                EmpleadoId = empleadoId,
                FechaHora = fechaHora,
                TipoRegistro = "Entrada",
                Metodo = "Biometrico"
            };
        }
    }
}
