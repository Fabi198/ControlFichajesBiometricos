using System;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using DevsFingerPrint.Infrastructure.Services;
using Xunit;

namespace DevsFingerPrint.Tests
{
    public class InstanciaUnicaAgenteTests
    {
        private const string Secreto = "secreto-prueba-estabilidad-9f3c";
        private const string Token = "token-prueba-estabilidad";

        [Fact]
        public void DosIniciosSimultaneos_ProducenUnaSolaInstancia()
        {
            string nombre = Nombre();
            InstanciaUnicaAgente primera = null;
            InstanciaUnicaAgente segunda = null;
            var barrera = new Barrier(2);
            var hiloA = new Thread(() =>
            {
                barrera.SignalAndWait();
                primera = InstanciaUnicaAgente.Adquirir(nombre);
            });
            var hiloB = new Thread(() =>
            {
                barrera.SignalAndWait();
                segunda = InstanciaUnicaAgente.Adquirir(nombre);
            });
            hiloA.Start();
            hiloB.Start();
            Assert.True(hiloA.Join(5000));
            Assert.True(hiloB.Join(5000));
            try
            {
                Assert.NotNull(primera);
                Assert.NotNull(segunda);
                Assert.NotEqual(primera.EsPropietaria, segunda.EsPropietaria);
            }
            finally
            {
                if (primera != null)
                {
                    primera.Dispose();
                }

                if (segunda != null)
                {
                    segunda.Dispose();
                }
            }
        }

        [Fact]
        public void SegundaInstancia_NoCreaNotifyIcon()
        {
            string nombre = Nombre();
            int iconos = 0;
            using (var principal = InstanciaUnicaAgente.Adquirir(nombre))
            {
                InstanciaUnicaAgente segunda = AdquirirEnOtroHilo(nombre);
                try
                {
                    Assert.True(principal.EsPropietaria);
                    Arrancar(principal, ref iconos);
                    Arrancar(segunda, ref iconos);
                    Assert.Equal(1, iconos);
                }
                finally
                {
                    if (segunda != null)
                    {
                        segunda.Dispose();
                    }
                }
            }
        }

        [Fact]
        public void TerceraYCuarta_NoDuplican()
        {
            string nombre = Nombre();
            int iconos = 0;
            using (var principal = InstanciaUnicaAgente.Adquirir(nombre))
            {
                InstanciaUnicaAgente tercera = AdquirirEnOtroHilo(nombre);
                InstanciaUnicaAgente cuarta = AdquirirEnOtroHilo(nombre);
                try
                {
                    Arrancar(principal, ref iconos);
                    Arrancar(tercera, ref iconos);
                    Arrancar(cuarta, ref iconos);
                    Assert.Equal(1, iconos);
                    Assert.False(tercera.EsPropietaria);
                    Assert.False(cuarta.EsPropietaria);
                }
                finally
                {
                    if (tercera != null)
                    {
                        tercera.Dispose();
                    }

                    if (cuarta != null)
                    {
                        cuarta.Dispose();
                    }
                }
            }
        }

        [Fact]
        public void MutexAbandonado_PermiteArrancar()
        {
            string nombre = Nombre();
            string listo = Path.Combine(Path.GetTempPath(), "cf-mutex-" + Guid.NewGuid().ToString("N") + ".ready");
            string script = Path.Combine(Path.GetTempPath(), "cf-mutex-" + Guid.NewGuid().ToString("N") + ".ps1");
            File.WriteAllText(script, ScriptQueRetieneMutex(nombre, listo));
            Process proceso = Process.Start(new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = "-NoProfile -ExecutionPolicy Bypass -File \"" + script + "\"",
                UseShellExecute = false,
                CreateNoWindow = true
            });
            try
            {
                var limite = DateTime.UtcNow.AddSeconds(15);
                while (!File.Exists(listo) && DateTime.UtcNow < limite)
                {
                    Thread.Sleep(50);
                }

                Assert.True(File.Exists(listo));
                using (InstanciaUnicaAgente ocupada = InstanciaUnicaAgente.Adquirir(nombre))
                {
                    Assert.False(ocupada.EsPropietaria);
                }

                proceso.Kill();
                proceso.WaitForExit(5000);
                using (InstanciaUnicaAgente siguiente = InstanciaUnicaAgente.Adquirir(nombre))
                {
                    Assert.True(siguiente.EsPropietaria);
                }
            }
            finally
            {
                if (proceso != null && !proceso.HasExited)
                {
                    proceso.Kill();
                }

                if (File.Exists(listo))
                {
                    File.Delete(listo);
                }

                if (File.Exists(script))
                {
                    File.Delete(script);
                }
            }
        }

        [Fact]
        public void Senal_LlegaALaInstanciaExistente()
        {
            string nombre = Nombre();
            int avisos = 0;
            using (var principal = InstanciaUnicaAgente.Adquirir(nombre))
            {
                principal.IniciarEscucha();
                principal.Escuchar(() => Interlocked.Increment(ref avisos));
                bool rechazo = false;
                var hilo = new Thread(() =>
                {
                    using (var segunda = InstanciaUnicaAgente.Adquirir(nombre))
                    {
                        rechazo = !InstanciaUnicaAgente.ResolverArranque(segunda.EsPropietaria, segunda.Senalar);
                    }
                });
                hilo.Start();
                Assert.True(hilo.Join(5000));
                Assert.True(rechazo);

                var limite = DateTime.UtcNow.AddSeconds(3);
                while (Volatile.Read(ref avisos) == 0 && DateTime.UtcNow < limite)
                {
                    Thread.Sleep(20);
                }

                Assert.Equal(1, avisos);
            }
        }

        [Fact]
        public void TaskbarCreated_NoCreaOtroIconoNiTimer()
        {
            var gestor = new GestorIconoBandeja();
            Assert.True(gestor.CrearIcono());
            gestor.RegistrarTimer();
            Assert.True(gestor.SuscribirBarra());
            Assert.False(gestor.SuscribirBarra());
            gestor.Republicar();
            Assert.Equal(1, gestor.IconosCreados);
            Assert.Equal(1, gestor.Timers);
            Assert.Equal(1, gestor.Republicaciones);
            Assert.True(gestor.Visible);
        }

        [Fact]
        public void MultiplesTaskbarCreated_SiguenEnUnaInstancia()
        {
            var gestor = new GestorIconoBandeja();
            gestor.CrearIcono();
            gestor.RegistrarTimer();
            gestor.RegistrarTimer();
            gestor.RegistrarTimer();
            gestor.Republicar();
            gestor.Republicar();
            gestor.Republicar();
            gestor.Republicar();
            Assert.Equal(1, gestor.IconosCreados);
            Assert.Equal(3, gestor.Timers);
            Assert.Equal(4, gestor.Republicaciones);
            Assert.True(gestor.Visible);
        }

        [Fact]
        public void Cierre_DisponeElIconoUnaVez()
        {
            var gestor = new GestorIconoBandeja();
            gestor.CrearIcono();
            gestor.Cerrar();
            gestor.Cerrar();
            Assert.Equal(1, gestor.Disposes);
            Assert.False(gestor.Visible);
            Assert.False(gestor.CrearIcono());
        }

        [Fact]
        public void InstaladorYTarea_NoGeneranDobleInicio()
        {
            string ruta = RutaScript();
            string script = File.ReadAllText(ruta);
            Assert.Contains("--origen=tarea", script);
            Assert.Contains("--origen=instalador", script);
            Assert.Contains("<MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy>", script);
            Assert.DoesNotContain("/Run", script);
            int ejecuciones = Regex.Matches(script, @"Exec\(Exe").Count;
            Assert.Equal(1, ejecuciones);
            Assert.Contains("Inicio unico por el ejecutable", script);
            Assert.Equal(OrigenInicio.Tarea, OrigenInicio.Resolver(new[] { "--origen=tarea" }));
            Assert.Equal(OrigenInicio.Instalador, OrigenInicio.Resolver(new[] { "--origen=instalador" }));
            Assert.Equal(OrigenInicio.Manual, OrigenInicio.Resolver(new[] { "DevsFingerPrint.exe" }));
        }

        [Fact]
        public void ReintentarConexion_NoSeSuperpone()
        {
            var manual = new ReintentoManual();
            int ejecuciones = 0;
            var entro = new ManualResetEventSlim(false);
            var seguir = new ManualResetEventSlim(false);
            Assert.True(manual.Iniciar(() =>
            {
                entro.Set();
                seguir.Wait();
                Interlocked.Increment(ref ejecuciones);
            }));
            Assert.True(entro.Wait(3000));
            Assert.False(manual.Iniciar(() => Interlocked.Increment(ref ejecuciones)));
            seguir.Set();
            var limite = DateTime.UtcNow.AddSeconds(3);
            while (Volatile.Read(ref ejecuciones) == 0 && DateTime.UtcNow < limite)
            {
                Thread.Sleep(20);
            }

            Assert.Equal(1, ejecuciones);
        }

        [Fact]
        public void Logs_NoContienenInformacionSensible()
        {
            long antes = TamanoLog();
            string inicio = RegistroOperativo.Inicio(7, "1.0.4.0", OrigenInicio.Manual);
            string existente = RegistroOperativo.InstanciaExistente(8);
            string senal = RegistroOperativo.Senal(7);
            AgentLog.Info(inicio);
            AgentLog.Info(existente);
            AgentLog.Info(senal);
            AgentLog.Info(RegistroOperativo.TaskbarCreada());
            AgentLog.Info(RegistroOperativo.IconoRepublicado());
            AgentLog.Info(RegistroOperativo.Fin(7));
            string agregado = TextoNuevo(antes);
            Assert.Contains("Se detectó una instancia existente. PID 8.", agregado);
            Assert.Contains("Mutex", RegistroOperativo.MutexAdquirido(7));
            Assert.DoesNotContain(Secreto, agregado);
            Assert.DoesNotContain(Token, agregado);
            Assert.DoesNotContain("TemplateBiometrico", agregado);
            Assert.DoesNotContain("ClientSecret", agregado);
            Assert.DoesNotContain("eyJ", agregado);
            Assert.Equal("Sin sincronizaciones", CicloVidaAgente.TextoUltimaSincronizacion(null));
            Assert.Equal("Última sincronización: 28/10/2026 00:40", CicloVidaAgente.TextoUltimaSincronizacion(new DateTime(2026, 10, 28, 0, 40, 0)));
            Assert.Equal("Versión 1.0.4", CicloVidaAgente.TextoVersion(new Version(1, 0, 4, 0)));
            Assert.EndsWith(Path.Combine("ControlFichajes", "Logs"), AgentLog.CarpetaLogs());
        }

        private static InstanciaUnicaAgente AdquirirEnOtroHilo(string nombre)
        {
            InstanciaUnicaAgente resultado = null;
            var hilo = new Thread(() => resultado = InstanciaUnicaAgente.Adquirir(nombre));
            hilo.Start();
            Assert.True(hilo.Join(5000));
            return resultado;
        }

        private static string ScriptQueRetieneMutex(string nombre, string listo)
        {
            return "$name = '" + nombre + "'\r\n"
                + "$ready = '" + listo + "'\r\n"
                + "Add-Type -TypeDefinition @'\r\n"
                + "using System;\r\n"
                + "using System.Runtime.InteropServices;\r\n"
                + "public class MutexHold {\r\n"
                + "  [DllImport(\"kernel32\", CharSet=CharSet.Unicode, SetLastError=true)]\r\n"
                + "  public static extern IntPtr CreateMutex(IntPtr atributos, bool propietario, string nombre);\r\n"
                + "  [DllImport(\"kernel32\")]\r\n"
                + "  public static extern uint WaitForSingleObject(IntPtr handle, uint milisegundos);\r\n"
                + "  public static uint Tomar(string nombre) {\r\n"
                + "    IntPtr handle = CreateMutex(IntPtr.Zero, false, nombre);\r\n"
                + "    return WaitForSingleObject(handle, 0);\r\n"
                + "  }\r\n"
                + "}\r\n"
                + "'@\r\n"
                + "$resultado = [MutexHold]::Tomar($name)\r\n"
                + "if ($resultado -eq 0) { Set-Content -Path $ready -Value 'ok' }\r\n"
                + "Start-Sleep -Seconds 30\r\n";
        }

        private static void Arrancar(InstanciaUnicaAgente instancia, ref int iconos)
        {
            if (!InstanciaUnicaAgente.ResolverArranque(instancia.EsPropietaria, instancia.Senalar))
            {
                return;
            }

            var gestor = new GestorIconoBandeja();
            if (gestor.CrearIcono())
            {
                iconos++;
            }
        }

        private static string Nombre()
        {
            return @"Local\DevsFingerPrint.Prueba." + Guid.NewGuid().ToString("N");
        }

        private static string RutaScript()
        {
            DirectoryInfo directorio = new DirectoryInfo(AppContext.BaseDirectory);
            while (directorio != null)
            {
                string candidato = Path.Combine(directorio.FullName, "innoDevs.iss");
                if (File.Exists(candidato))
                {
                    return candidato;
                }

                directorio = directorio.Parent;
            }

            throw new FileNotFoundException("No se encontro innoDevs.iss.");
        }

        private static long TamanoLog()
        {
            string ruta = AgentLog.RutaArchivoHoy();
            return File.Exists(ruta) ? new FileInfo(ruta).Length : 0;
        }

        private static string TextoNuevo(long desde)
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
    }
}
