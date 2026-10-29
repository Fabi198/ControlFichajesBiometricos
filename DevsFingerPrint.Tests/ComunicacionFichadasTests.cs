using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DevsFingerPrint.Domain.Models;
using DevsFingerPrint.Infrastructure.Data;
using DevsFingerPrint.Infrastructure.Repositories;
using DevsFingerPrint.Infrastructure.Services;
using Xunit;

namespace DevsFingerPrint.Tests
{
    public class ComunicacionFichadasTests
    {
        [Fact]
        public void IntervalosDeLaVersion_QuedanFijos()
        {
            Assert.Equal(60000, CicloVidaAgente.IntervaloHeartbeatNormalMs);
            Assert.Equal(1200000, CicloVidaAgente.IntervaloSincronizacionNormalMs);
            Assert.Equal(60000, CicloVidaAgente.IntervaloRecuperacionFichadasNormalMs);
            Assert.Equal(60000, CicloVidaAgente.IntervaloReintentoMs(1));
            Assert.Equal(120000, CicloVidaAgente.IntervaloReintentoMs(2));
            Assert.Equal(240000, CicloVidaAgente.IntervaloReintentoMs(3));
            Assert.Equal(300000, CicloVidaAgente.IntervaloReintentoMs(4));
            Assert.Equal(300000, CicloVidaAgente.IntervaloReintentoMs(8));
        }

        [Fact]
        public void FalloDeHeartbeat_NoAceleraElCatalogo()
        {
            var heartbeat = new PoliticaIntervalo(CicloVidaAgente.IntervaloHeartbeatNormalMs, false);
            var catalogo = new PoliticaIntervalo(CicloVidaAgente.IntervaloSincronizacionNormalMs, true);
            int catalogoInicial = catalogo.IntervaloMs;

            heartbeat.RegistrarFalloTemporal();
            heartbeat.RegistrarFalloTemporal();
            heartbeat.RegistrarFalloTemporal();

            Assert.True(heartbeat.IntervaloMs > CicloVidaAgente.IntervaloHeartbeatNormalMs);
            Assert.Equal(catalogoInicial, catalogo.IntervaloMs);
            Assert.Equal(CicloVidaAgente.IntervaloSincronizacionNormalMs, catalogo.IntervaloMs);
        }

        [Fact]
        public void FalloDelCatalogo_NoCambiaElHeartbeatNiLoAcorta()
        {
            var heartbeat = new PoliticaIntervalo(CicloVidaAgente.IntervaloHeartbeatNormalMs, false);
            var catalogo = new PoliticaIntervalo(CicloVidaAgente.IntervaloSincronizacionNormalMs, true);

            catalogo.RegistrarFalloTemporal();
            catalogo.RegistrarFalloTemporal();

            Assert.Equal(CicloVidaAgente.IntervaloHeartbeatNormalMs, heartbeat.IntervaloMs);
            Assert.Equal(0, heartbeat.Fallos);
            Assert.Equal(CicloVidaAgente.IntervaloSincronizacionNormalMs, catalogo.IntervaloMs);
        }

        [Fact]
        public void ColaVacia_NoConsultaElServidor()
        {
            int envios = 0;
            var envio = new EnvioFichadasPendientes(
                () => new Fichada[0],
                lote =>
                {
                    Interlocked.Increment(ref envios);
                    return EstadoEnvioFichadas.Confirmado;
                },
                ids => { });

            ResultadoEnvioFichadas resultado = envio.Ejecutar();

            Assert.Equal(EstadoEnvioFichadas.SinPendientes, resultado.Estado);
            Assert.False(resultado.ConsultoServidor);
            Assert.Equal(0, envios);
        }

        [Fact]
        public void Envio_RespetaOrdenYLotesDe500()
        {
            var pendientes = new List<Fichada>();
            for (int i = 0; i < 501; i++)
            {
                pendientes.Add(new Fichada
                {
                    Id = i + 1,
                    EmpleadoId = 4,
                    FechaHora = new DateTime(2026, 10, 12, 8, 0, 0).AddMinutes(i),
                    TipoRegistro = "Entrada",
                    Metodo = "Biometrico"
                });
            }

            var enviados = new List<IReadOnlyList<Fichada>>();
            var envio = new EnvioFichadasPendientes(
                () => pendientes.Where(item => !item.Sincronizado).ToList(),
                lote =>
                {
                    enviados.Add(lote.ToList());
                    return EstadoEnvioFichadas.Confirmado;
                },
                ids =>
                {
                    foreach (Fichada fichada in pendientes)
                    {
                        if (ids.Contains(fichada.Id))
                        {
                            fichada.Sincronizado = true;
                        }
                    }
                });

            ResultadoEnvioFichadas primero = envio.Ejecutar();
            ResultadoEnvioFichadas segundo = envio.Ejecutar();

            Assert.Equal(EstadoEnvioFichadas.Confirmado, primero.Estado);
            Assert.Equal(500, primero.Cantidad);
            Assert.Equal(new[] { 1, 500 }, new[] { enviados[0][0].Id, enviados[0][499].Id });
            Assert.Equal(501, enviados[1].Single().Id);
            Assert.Equal(1, segundo.Cantidad);
        }

        [Fact]
        public async Task EnvioConcurrente_NoDuplicaElLote()
        {
            var puerta = new ManualResetEventSlim(false);
            int envios = 0;
            var pendiente = CrearFichada(1, 9);
            pendiente.Id = 1;
            var envio = new EnvioFichadasPendientes(
                () => pendiente.Sincronizado ? new Fichada[0] : new[] { pendiente },
                lote =>
                {
                    Interlocked.Increment(ref envios);
                    puerta.Wait(TimeSpan.FromSeconds(5));
                    return EstadoEnvioFichadas.Confirmado;
                },
                ids => pendiente.Sincronizado = true);

            Task<ResultadoEnvioFichadas> primero = Task.Run(() => envio.Ejecutar());
            ResultadoEnvioFichadas segundo;
            try
            {
                Assert.True(SpinWait.SpinUntil(() => Volatile.Read(ref envios) == 1, TimeSpan.FromSeconds(2)));
                segundo = envio.Ejecutar();
            }
            finally
            {
                puerta.Set();
            }

            ResultadoEnvioFichadas primeroResultado = await primero;

            Assert.Equal(EstadoEnvioFichadas.Ocupado, segundo.Estado);
            Assert.False(segundo.ConsultoServidor);
            Assert.Equal(EstadoEnvioFichadas.Confirmado, primeroResultado.Estado);
            Assert.Equal(1, envios);
        }

        [Fact]
        public void FalloTemporalYRecuperacion_ConservanYLuegoConfirmanLaCola()
        {
            string directorio = Path.Combine(Path.GetTempPath(), "DevsFingerPrint.Tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directorio);
            string baseDatos = Path.Combine(directorio, "test.db");
            try
            {
                LocalDatabase.Inicializar(baseDatos);
                var repositorio = new FichadaRepository(LocalDatabase.CrearConnectionString(baseDatos, pooling: false));
                repositorio.GuardarFichadaLocal(CrearFichada(8, 9));
                int intentos = 0;
                var envio = new EnvioFichadasPendientes(
                    () => repositorio.ObtenerFichadasPendientes(),
                    lote =>
                    {
                        intentos++;
                        return intentos == 1 ? EstadoEnvioFichadas.FalloTemporal : EstadoEnvioFichadas.Confirmado;
                    },
                    ids => repositorio.MarcarComoSincronizadas(ids));

                ResultadoEnvioFichadas fallo = envio.Ejecutar();
                Assert.Equal(EstadoEnvioFichadas.FalloTemporal, fallo.Estado);
                Assert.Single(repositorio.ObtenerFichadasPendientes());

                ResultadoEnvioFichadas recuperacion = envio.Ejecutar();
                Assert.Equal(EstadoEnvioFichadas.Confirmado, recuperacion.Estado);
                Assert.Empty(repositorio.ObtenerFichadasPendientes());
            }
            finally
            {
                System.Data.SQLite.SQLiteConnection.ClearAllPools();
                if (Directory.Exists(directorio))
                {
                    Directory.Delete(directorio, true);
                }
            }
        }

        [Fact]
        public async Task RedCaidaYHttp500_ClasificanElEnvioComoTemporal()
        {
            var sinRed = new ApiClient("http://127.0.0.1:1");
            Assert.Equal(EstadoEnvioFichadas.Fallo, sinRed.ClasificarEnvioFichadas(new[] { CrearFichada(1, 9) }));

            string baseUrl = CrearBaseUrl();
            using (var listener = new HttpListener())
            {
                listener.Prefixes.Add(baseUrl + "/");
                listener.Start();
                var server = AtenderAsync(listener);
                var client = new ApiClient(baseUrl);
                Assert.True(client.IniciarSesionAgente("cliente-prueba", "secreto-prueba"));
                Assert.Equal(
                    EstadoEnvioFichadas.FalloTemporal,
                    client.ClasificarEnvioFichadas(new[] { CrearFichada(3, 10) }));
                listener.Stop();
                Assert.Equal(
                    EstadoEnvioFichadas.FalloTemporal,
                    client.ClasificarEnvioFichadas(new[] { CrearFichada(3, 11) }));
                await server;
            }
        }

        [Fact]
        public async Task FichadaDuranteEnvioActivo_QuedaPendienteYSeEnviaDespues()
        {
            var pendientes = new List<Fichada> { CrearFichada(4, 8) };
            pendientes[0].Id = 1;
            var puerta = new ManualResetEventSlim(false);
            int envios = 0;
            var envio = new EnvioFichadasPendientes(
                () => pendientes.Where(item => !item.Sincronizado).Select(item => item).ToList(),
                lote =>
                {
                    int actual = Interlocked.Increment(ref envios);
                    if (actual == 1)
                    {
                        puerta.Wait(TimeSpan.FromSeconds(5));
                    }

                    return EstadoEnvioFichadas.Confirmado;
                },
                ids =>
                {
                    foreach (Fichada fichada in pendientes)
                    {
                        if (ids.Contains(fichada.Id))
                        {
                            fichada.Sincronizado = true;
                        }
                    }
                });

            Task<ResultadoEnvioFichadas> evento = Task.Run(() => envio.Ejecutar());
            try
            {
                Assert.True(SpinWait.SpinUntil(() => Volatile.Read(ref envios) == 1, TimeSpan.FromSeconds(2)));
                pendientes.Add(new Fichada
                {
                    Id = 2,
                    EmpleadoId = 4,
                    FechaHora = new DateTime(2026, 10, 12, 9, 0, 0),
                    TipoRegistro = "Salida",
                    Metodo = "Biometrico"
                });
                ResultadoEnvioFichadas temporizador = envio.Ejecutar();
                Assert.Equal(EstadoEnvioFichadas.Ocupado, temporizador.Estado);
                Assert.False(temporizador.ConsultoServidor);
            }
            finally
            {
                puerta.Set();
            }

            ResultadoEnvioFichadas resultado = await evento;
            Assert.Equal(EstadoEnvioFichadas.Confirmado, resultado.Estado);
            Assert.Equal(2, Volatile.Read(ref envios));
            Assert.All(pendientes, item => Assert.True(item.Sincronizado));
        }

        [Fact]
        public async Task FalloTemporalDuranteEnvio_NoRepiteLaSolicitudEnElMismoCiclo()
        {
            var pendientes = new List<Fichada> { CrearFichada(4, 8) };
            pendientes[0].Id = 1;
            var puerta = new ManualResetEventSlim(false);
            int envios = 0;
            var envio = new EnvioFichadasPendientes(
                () => pendientes.Where(item => !item.Sincronizado).ToList(),
                lote =>
                {
                    if (Interlocked.Increment(ref envios) == 1)
                    {
                        puerta.Wait(TimeSpan.FromSeconds(5));
                    }

                    return EstadoEnvioFichadas.FalloTemporal;
                },
                ids => pendientes[0].Sincronizado = true);

            Task<ResultadoEnvioFichadas> evento = Task.Run(() => envio.Ejecutar());
            try
            {
                Assert.True(SpinWait.SpinUntil(() => Volatile.Read(ref envios) == 1, TimeSpan.FromSeconds(2)));
                Assert.Equal(EstadoEnvioFichadas.Ocupado, envio.Ejecutar().Estado);
            }
            finally
            {
                puerta.Set();
            }

            ResultadoEnvioFichadas resultado = await evento;
            Assert.Equal(EstadoEnvioFichadas.FalloTemporal, resultado.Estado);
            Assert.Equal(1, Volatile.Read(ref envios));
            Assert.False(pendientes[0].Sincronizado);
        }

        [Fact]
        public void DesconexionProlongada_AlargaElReintentoYLaRecuperacionVuelveAlIntervalo()
        {
            var fichadas = new PoliticaIntervalo(CicloVidaAgente.IntervaloRecuperacionFichadasNormalMs, false);
            var heartbeat = new PoliticaIntervalo(CicloVidaAgente.IntervaloHeartbeatNormalMs, false);
            var catalogo = new PoliticaIntervalo(CicloVidaAgente.IntervaloSincronizacionNormalMs, true);
            for (int fallo = 0; fallo < 5; fallo++)
            {
                fichadas.RegistrarFalloTemporal();
                heartbeat.RegistrarFalloTemporal();
                catalogo.RegistrarFalloTemporal();
            }

            Assert.Equal(300000, fichadas.IntervaloMs);
            Assert.Equal(300000, heartbeat.IntervaloMs);
            Assert.Equal(CicloVidaAgente.IntervaloSincronizacionNormalMs, catalogo.IntervaloMs);

            fichadas.RegistrarRecuperacion();
            heartbeat.RegistrarRecuperacion();
            Assert.Equal(CicloVidaAgente.IntervaloRecuperacionFichadasNormalMs, fichadas.IntervaloMs);
            Assert.Equal(CicloVidaAgente.IntervaloHeartbeatNormalMs, heartbeat.IntervaloMs);
            Assert.Equal(CicloVidaAgente.IntervaloSincronizacionNormalMs, catalogo.IntervaloMs);
        }

        [Fact]
        public async Task AutenticacionRechazada_NoSigueConsultandoElServidor()
        {
            int solicitudes = 0;
            string baseUrl = CrearBaseUrl();
            using (var listener = new HttpListener())
            {
                listener.Prefixes.Add(baseUrl + "/");
                listener.Start();
                var server = AtenderAutenticacionFallidaAsync(listener, () => Interlocked.Increment(ref solicitudes));
                var client = new ApiClient(baseUrl);
                Assert.True(client.IniciarSesionAgente("cliente-prueba", "secreto-prueba"));
                int despuesDelLogin = Volatile.Read(ref solicitudes);

                Assert.False(client.EnviarHeartbeat());
                Assert.True(client.CredencialesRechazadas);
                Assert.Equal(EstadoEnvioFichadas.Fallo, client.ClasificarEnvioFichadas(new[] { CrearFichada(3, 10) }));
                int despuesDelRechazo = Volatile.Read(ref solicitudes);

                Assert.False(client.EnviarHeartbeat());
                Assert.Equal(EstadoEnvioFichadas.Fallo, client.ClasificarEnvioFichadas(new[] { CrearFichada(3, 11) }));
                Assert.Equal(despuesDelRechazo, Volatile.Read(ref solicitudes));
                Assert.True(despuesDelRechazo > despuesDelLogin);

                listener.Stop();
                await server;
            }
        }

        [Fact]
        public void EncolarTrabajo_RegresaAntesDeQueTermineElEnvio()
        {
            var entro = new ManualResetEventSlim(false);
            var seguir = new ManualResetEventSlim(false);
            var reloj = System.Diagnostics.Stopwatch.StartNew();
            ColaTrabajoAgente.Encolar(() =>
            {
                entro.Set();
                seguir.Wait(TimeSpan.FromSeconds(5));
            });

            Assert.True(entro.Wait(TimeSpan.FromSeconds(2)));
            Assert.True(reloj.Elapsed < TimeSpan.FromSeconds(2));
            seguir.Set();
        }

        [Fact]
        public void ExcepcionDeEnvio_LiberaLaExclusionParaElSiguienteIntento()
        {
            int envios = 0;
            var envio = new EnvioFichadasPendientes(
                () => new[] { CrearFichada(1, 9) },
                lote =>
                {
                    if (Interlocked.Increment(ref envios) == 1)
                    {
                        throw new IOException("red");
                    }

                    return EstadoEnvioFichadas.Confirmado;
                },
                ids => { });

            Assert.Throws<IOException>(() => envio.Ejecutar());
            Assert.Equal(EstadoEnvioFichadas.Confirmado, envio.Ejecutar().Estado);
            Assert.Equal(2, envios);
        }

        [Fact]
        public void ReintentoDeFichadas_CreceSinMoverLosOtrosCanales()
        {
            var fichadas = new PoliticaIntervalo(CicloVidaAgente.IntervaloRecuperacionFichadasNormalMs, false);
            var catalogo = new PoliticaIntervalo(CicloVidaAgente.IntervaloSincronizacionNormalMs, true);
            var heartbeat = new PoliticaIntervalo(CicloVidaAgente.IntervaloHeartbeatNormalMs, false);

            fichadas.RegistrarFalloTemporal();
            fichadas.RegistrarFalloTemporal();

            Assert.Equal(120000, fichadas.IntervaloMs);
            Assert.Equal(CicloVidaAgente.IntervaloHeartbeatNormalMs, heartbeat.IntervaloMs);
            Assert.Equal(CicloVidaAgente.IntervaloSincronizacionNormalMs, catalogo.IntervaloMs);
            fichadas.RegistrarRecuperacion();
            Assert.Equal(CicloVidaAgente.IntervaloRecuperacionFichadasNormalMs, fichadas.IntervaloMs);
        }

        private static Fichada CrearFichada(int empleadoId, int hora)
        {
            return new Fichada
            {
                EmpleadoId = empleadoId,
                FechaHora = new DateTime(2026, 10, 12, hora, 0, 0),
                TipoRegistro = "Entrada",
                Metodo = "Biometrico"
            };
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

        private static async Task AtenderAutenticacionFallidaAsync(HttpListener listener, Func<int> contar)
        {
            int logins = 0;
            try
            {
                while (listener.IsListening)
                {
                    HttpListenerContext context = await listener.GetContextAsync();
                    contar();
                    bool esLogin = context.Request.Url.AbsolutePath.IndexOf("auth", StringComparison.OrdinalIgnoreCase) >= 0;
                    bool aceptarLogin = esLogin && Interlocked.Increment(ref logins) == 1;
                    string cuerpo = aceptarLogin ? "{\"token\":\"" + TokenAgente() + "\"}" : "{}";
                    byte[] bytes = Encoding.UTF8.GetBytes(cuerpo);
                    context.Response.StatusCode = aceptarLogin ? 200 : 401;
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

        private static string TokenAgente()
        {
            string json = "{\"agente_id\":7,\"empresa_id\":3}";
            string payload = Convert.ToBase64String(Encoding.UTF8.GetBytes(json)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
            return "eyJhbGciOiJub25lIn0." + payload + ".firma";
        }

        private static async Task AtenderAsync(HttpListener listener)
        {
            try
            {
                while (listener.IsListening)
                {
                    HttpListenerContext context = await listener.GetContextAsync();
                    bool esLogin = context.Request.Url.AbsolutePath.IndexOf("auth", StringComparison.OrdinalIgnoreCase) >= 0;
                    string cuerpo = esLogin
                        ? "{\"token\":\"header.eyJhZ2VudGVfaWQiOjd9.cuerpo\"}"
                        : "{\"mensaje\":\"no disponible\"}";
                    int estado = esLogin ? 200 : 500;
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
    }
}
