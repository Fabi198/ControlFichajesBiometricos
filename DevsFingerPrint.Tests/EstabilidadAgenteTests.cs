using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DevsFingerPrint.Infrastructure.Services;
using Xunit;

namespace DevsFingerPrint.Tests
{
    public class EstabilidadAgenteTests
    {
        private const string Secreto = "secreto-prueba-estabilidad-9f3c";
        private const string MarcaToken = "token-prueba-estabilidad";

        [Fact]
        public async Task Heartbeat401_RenuevaUnaVezYReintenta()
        {
            string tokenInicial = Token("inicial");
            string tokenNuevo = Token(MarcaToken);
            int logins = 0;
            int heartbeats = 0;
            string baseUrl = CrearBaseUrl();
            using (var listener = new HttpListener())
            {
                listener.Prefixes.Add(baseUrl + "/");
                listener.Start();
                var server = AtenderAsync(listener, () =>
                {
                    int actual = Interlocked.Increment(ref logins);
                    return actual == 1 ? tokenInicial : tokenNuevo;
                }, () => Interlocked.Increment(ref heartbeats), tokenNuevo, false);
                ApiClient client = CrearCliente(baseUrl);

                Assert.True(client.IniciarSesionAgente("cliente-prueba", Secreto));
                Assert.True(client.EnviarHeartbeat());
                listener.Stop();
                await IgnorarCierre(server);

                Assert.Equal(2, logins);
                Assert.False(client.CredencialesRechazadas);
                Assert.DoesNotContain(Secreto, TextoNuevoDelLog(0));
            }
        }

        [Fact]
        public async Task HeartbeatYSincronizacion401_HacenUnSoloLogin()
        {
            string tokenInicial = Token("inicial");
            string tokenNuevo = Token(MarcaToken);
            int logins = 0;
            string baseUrl = CrearBaseUrl();
            using (var listener = new HttpListener())
            {
                listener.Prefixes.Add(baseUrl + "/");
                listener.Start();
                var server = AtenderAsync(listener, () =>
                {
                    int actual = Interlocked.Increment(ref logins);
                    return actual == 1 ? tokenInicial : tokenNuevo;
                }, () => 0, tokenNuevo, false);
                ApiClient client = CrearCliente(baseUrl);
                Assert.True(client.IniciarSesionAgente("cliente-prueba", Secreto));

                Task<bool> heartbeat = Task.Run(() => client.EnviarHeartbeat());
                Task<ResultadoLista<Domain.Models.Empleado>> empleados = Task.Run(() => client.ObtenerEmpleados());
                await Task.WhenAll(heartbeat, empleados);
                listener.Stop();
                await IgnorarCierre(server);

                Assert.True(await heartbeat);
                Assert.True((await empleados).Exitoso);
                Assert.Equal(2, logins);
            }
        }

        [Fact]
        public async Task RenovacionFallida_NoReintentaElLogin()
        {
            string tokenInicial = Token("inicial");
            int logins = 0;
            string baseUrl = CrearBaseUrl();
            using (var listener = new HttpListener())
            {
                listener.Prefixes.Add(baseUrl + "/");
                listener.Start();
                var server = AtenderAsync(listener, () =>
                {
                    return Interlocked.Increment(ref logins) == 1 ? tokenInicial : null;
                }, () => 0, Token("nunca"), true);
                ApiClient client = CrearCliente(baseUrl);
                Assert.True(client.IniciarSesionAgente("cliente-prueba", Secreto));

                Assert.False(client.EnviarHeartbeat());
                Assert.False(client.EnviarHeartbeat());
                listener.Stop();
                await IgnorarCierre(server);

                Assert.Equal(2, logins);
                Assert.True(client.CredencialesRechazadas);
            }
        }

        [Fact]
        public void CredencialesInvalidasYCancelarLogin_NoTerminanElProceso()
        {
            Assert.False(CicloVidaAgente.DebeTerminarProceso(false, true));
            Assert.False(CicloVidaAgente.DebeTerminarProceso(false, false));
            Assert.Equal(
                EstadoOperativo.CredencialesInvalidas,
                CicloVidaAgente.ResolverEstado(true, false, false, true));
        }

        [Fact]
        public async Task TimeoutRedY500_MantienenLaSesionReintentable()
        {
            Assert.True(CicloVidaAgente.EsFalloTemporal("timeout"));
            Assert.True(CicloVidaAgente.EsFalloTemporal("red"));
            Assert.True(CicloVidaAgente.EsFalloTemporal("http-429"));
            Assert.True(CicloVidaAgente.EsFalloTemporal("http-500"));
            Assert.False(CicloVidaAgente.EsFalloTemporal("http-401"));
            Assert.Equal("timeout", ApiClient.MotivoTransporte(new WebException("demora", WebExceptionStatus.Timeout)));
            Assert.Equal("red", ApiClient.MotivoTransporte(new WebException("caida", WebExceptionStatus.ConnectFailure)));

            string token = Token("inicial");
            int heartbeats = 0;
            string baseUrl = CrearBaseUrl();
            using (var listener = new HttpListener())
            {
                listener.Prefixes.Add(baseUrl + "/");
                listener.Start();
                var server = AtenderAsync(listener, () => token, () => Interlocked.Increment(ref heartbeats), token, false, HttpStatusCode.InternalServerError);
                ApiClient client = CrearCliente(baseUrl);
                Assert.True(client.IniciarSesionAgente("cliente-prueba", Secreto));
                Assert.False(client.EnviarHeartbeat());
                Assert.False(client.CredencialesRechazadas);
                Assert.False(client.EnviarHeartbeat());
                listener.Stop();
                await IgnorarCierre(server);
                Assert.Equal(2, heartbeats);
            }
        }

        [Fact]
        public void TimerYExcepcionPeriodica_NoSeSuperponenNiEscapan()
        {
            var operacion = new OperacionExclusiva();
            Assert.True(operacion.TryEnter());
            Assert.False(operacion.TryEnter());
            operacion.Salir();
            Assert.True(operacion.TryEnter());
            operacion.Salir();

            Exception capturada = null;
            operacion.Ejecutar(() => { throw new InvalidOperationException("fallo-periodico"); }, ex => capturada = ex);
            Assert.NotNull(capturada);
            Assert.False(operacion.EnCurso);
            Assert.True(operacion.TryEnter());
            operacion.Salir();
        }

        [Fact]
        public void Recuperacion_VuelveAlIntervaloNormal()
        {
            var heartbeat = new PoliticaIntervalo(CicloVidaAgente.IntervaloHeartbeatNormalMs, false);
            var catalogo = new PoliticaIntervalo(CicloVidaAgente.IntervaloSincronizacionNormalMs, true);
            heartbeat.RegistrarFalloTemporal();
            heartbeat.RegistrarFalloTemporal();
            catalogo.RegistrarFalloTemporal();
            Assert.True(heartbeat.IntervaloMs > CicloVidaAgente.IntervaloHeartbeatNormalMs);
            Assert.Equal(CicloVidaAgente.IntervaloSincronizacionNormalMs, catalogo.IntervaloMs);
            heartbeat.RegistrarRecuperacion();
            catalogo.RegistrarRecuperacion();
            Assert.Equal(CicloVidaAgente.IntervaloHeartbeatNormalMs, heartbeat.IntervaloMs);
            Assert.Equal(CicloVidaAgente.IntervaloSincronizacionNormalMs, catalogo.IntervaloMs);
            Assert.Equal(EstadoOperativo.Conectado, CicloVidaAgente.ResolverEstado(false, false, true, true));
        }

        [Fact]
        public void Cierres_GeneranTextosDistintos()
        {
            string usuario = CicloVidaAgente.TextoCierre(MotivoCierre.Usuario);
            string sesion = CicloVidaAgente.TextoCierre(MotivoCierre.SesionWindows);
            string contexto = CicloVidaAgente.TextoCierre(MotivoCierre.Contexto);
            Assert.Equal("Salida solicitada por el usuario.", usuario);
            Assert.Equal("Cierre de sesión o apagado de Windows.", sesion);
            Assert.Equal("Finalización normal del contexto.", contexto);
            Assert.NotEqual(usuario, sesion);
            Assert.NotEqual(usuario, contexto);
        }

        [Fact]
        public void TaskbarCreated_RepublicaElMismoIcono()
        {
            Assert.True(CicloVidaAgente.EsMensajeBarraTareas(1200, 1200));
            Assert.False(CicloVidaAgente.EsMensajeBarraTareas(15, 1200));
            int ocultos = 0;
            int visibles = 0;
            CicloVidaAgente.RepublicarIcono(() => ocultos++, () => visibles++);
            Assert.Equal(1, ocultos);
            Assert.Equal(1, visibles);
        }

        [Fact]
        public async Task LogsDeRenovacion_NoContienenSecretoNiToken()
        {
            long antes = TamanoLog();
            string token = Token(MarcaToken);
            string baseUrl = CrearBaseUrl();
            using (var listener = new HttpListener())
            {
                listener.Prefixes.Add(baseUrl + "/");
                listener.Start();
                var server = AtenderAsync(listener, () => token, () => 0, token, false);
                ApiClient client = CrearCliente(baseUrl);
                Assert.True(client.IniciarSesionAgente("cliente-prueba", Secreto));
                Assert.True(client.EnviarHeartbeat());
                listener.Stop();
                await IgnorarCierre(server);
            }

            string agregado = TextoNuevoDelLog(antes);
            Assert.DoesNotContain(Secreto, agregado);
            Assert.DoesNotContain(token, agregado);
            Assert.DoesNotContain("TemplateBiometrico", agregado);
            Assert.DoesNotContain("QUERTY", agregado);
        }

        private static ApiClient CrearCliente(string baseUrl)
        {
            ApiClient client = null;
            client = new ApiClient(baseUrl, () => client.IniciarSesionAgente("cliente-prueba", Secreto));
            return client;
        }

        private static string Token(string marca)
        {
            string json = "{\"agente_id\":7,\"empresa_id\":3,\"marca\":\"" + marca + "\"}";
            string payload = Convert.ToBase64String(Encoding.UTF8.GetBytes(json)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
            return "eyJhbGciOiJub25lIn0." + payload + "." + marca;
        }

        private static async Task AtenderAsync(
            HttpListener listener,
            Func<string> siguienteToken,
            Func<int> contarOperacion,
            string tokenValido,
            bool rechazarRenovacion,
            HttpStatusCode estadoOperacion = HttpStatusCode.OK)
        {
            try
            {
                while (listener.IsListening)
                {
                    HttpListenerContext context = await listener.GetContextAsync();
                    bool esLogin = context.Request.Url.AbsolutePath.IndexOf("auth", StringComparison.OrdinalIgnoreCase) >= 0;
                    string cuerpo;
                    int estado;
                    if (esLogin)
                    {
                    string token = siguienteToken();
                    if (token == null)
                    {
                        cuerpo = "{}";
                        estado = 401;
                    }
                        else
                        {
                            cuerpo = "{\"token\":\"" + token + "\"}";
                            estado = 200;
                        }
                    }
                    else
                    {
                        contarOperacion();
                        string autorizacion = context.Request.Headers["Authorization"] ?? string.Empty;
                        bool autorizado = autorizacion.IndexOf(tokenValido, StringComparison.Ordinal) >= 0;
                        if (!autorizado)
                        {
                            cuerpo = "{}";
                            estado = 401;
                        }
                        else if (context.Request.Url.AbsolutePath.IndexOf("empleados", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            cuerpo = "[]";
                            estado = 200;
                        }
                        else
                        {
                            cuerpo = "{\"ok\":true}";
                            estado = (int)estadoOperacion;
                        }
                    }

                    byte[] bytes = Encoding.UTF8.GetBytes(cuerpo);
                    context.Response.StatusCode = estado;
                    context.Response.ContentType = "application/json";
                    context.Response.ContentLength64 = bytes.Length;
                    await context.Response.OutputStream.WriteAsync(bytes, 0, bytes.Length);
                    context.Response.Close();
                }
            }
            catch (Exception)
            {
            }
        }

        private static async Task IgnorarCierre(Task server)
        {
            Task terminado = await Task.WhenAny(server, Task.Delay(1000));
            if (terminado != server)
            {
                return;
            }

            await server;
        }

        private static long TamanoLog()
        {
            string ruta = AgentLog.RutaArchivoHoy();
            return File.Exists(ruta) ? new FileInfo(ruta).Length : 0;
        }

        private static string TextoNuevoDelLog(long desde)
        {
            string ruta = AgentLog.RutaArchivoHoy();
            if (!File.Exists(ruta))
            {
                return string.Empty;
            }

            using (var stream = new FileStream(ruta, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                if (desde > stream.Length)
                {
                    desde = 0;
                }

                stream.Seek(desde, SeekOrigin.Begin);
                using (var reader = new StreamReader(stream))
                {
                    return reader.ReadToEnd();
                }
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
