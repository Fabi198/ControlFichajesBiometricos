using System;

namespace DevsFingerPrint.Infrastructure.Services
{
    public enum EstadoOperativo
    {
        Conectado,
        SinConexion,
        CredencialesInvalidas,
        LectorDesconectado,
        Sincronizando
    }

    public enum MotivoCierre
    {
        Usuario,
        SesionWindows,
        Contexto,
        ExcepcionNoControlada
    }

    public static class CicloVidaAgente
    {
        public const int IntervaloHeartbeatNormalMs = 60000;
        public const int IntervaloSincronizacionNormalMs = 1200000;
        public const int IntervaloRecuperacionFichadasNormalMs = 60000;
        public const int TamanoLoteFichadas = 500;

        public static bool DebeTerminarProceso(bool autenticado, bool usuarioCanceloFormulario)
        {
            return false;
        }

        public static EstadoOperativo ResolverEstado(bool credencialesInvalidas, bool sincronizando, bool apiConectada, bool lectorConectado)
        {
            if (credencialesInvalidas)
            {
                return EstadoOperativo.CredencialesInvalidas;
            }

            if (sincronizando)
            {
                return EstadoOperativo.Sincronizando;
            }

            if (!apiConectada)
            {
                return EstadoOperativo.SinConexion;
            }

            if (!lectorConectado)
            {
                return EstadoOperativo.LectorDesconectado;
            }

            return EstadoOperativo.Conectado;
        }

        public static string TextoEstado(EstadoOperativo estado)
        {
            if (estado == EstadoOperativo.Conectado)
            {
                return "Conectado";
            }

            if (estado == EstadoOperativo.SinConexion)
            {
                return "Sin conexión";
            }

            if (estado == EstadoOperativo.CredencialesInvalidas)
            {
                return "Credenciales no válidas";
            }

            if (estado == EstadoOperativo.LectorDesconectado)
            {
                return "Lector desconectado";
            }

            return "Sincronizando";
        }

        public static string TextoCierre(MotivoCierre motivo)
        {
            if (motivo == MotivoCierre.Usuario)
            {
                return "Salida solicitada por el usuario.";
            }

            if (motivo == MotivoCierre.SesionWindows)
            {
                return "Cierre de sesión o apagado de Windows.";
            }

            if (motivo == MotivoCierre.ExcepcionNoControlada)
            {
                return "Excepcion no controlada.";
            }

            return "Finalización normal del contexto.";
        }

        public static int IntervaloReintentoMs(int fallosConsecutivos)
        {
            if (fallosConsecutivos <= 1)
            {
                return 60000;
            }

            if (fallosConsecutivos == 2)
            {
                return 120000;
            }

            if (fallosConsecutivos == 3)
            {
                return 240000;
            }

            return 300000;
        }

        public static bool EsFalloTemporal(string motivo)
        {
            if (motivo == "timeout" || motivo == "red")
            {
                return true;
            }

            if (string.IsNullOrEmpty(motivo) || !motivo.StartsWith("http-", StringComparison.Ordinal))
            {
                return false;
            }

            int codigo;
            if (!int.TryParse(motivo.Substring(5), out codigo))
            {
                return false;
            }

            return codigo == 429 || (codigo >= 500 && codigo <= 599);
        }

        public static bool EsMensajeBarraTareas(int mensaje, int mensajeRegistrado)
        {
            return mensajeRegistrado != 0 && mensaje == mensajeRegistrado;
        }

        public static void RepublicarIcono(Action ocultar, Action mostrar)
        {
            if (ocultar == null || mostrar == null)
            {
                return;
            }

            ocultar();
            mostrar();
        }

        public static string TextoUltimaSincronizacion(DateTime? ultima)
        {
            if (ultima == null)
            {
                return "Sin sincronizaciones";
            }

            return "Última sincronización: " + ultima.Value.ToString("dd'/'MM'/'yyyy HH:mm", System.Globalization.CultureInfo.InvariantCulture);
        }

        public static string TextoVersion(Version version)
        {
            if (version == null)
            {
                return "Versión";
            }

            return "Versión " + version.ToString(3);
        }
    }

    public static class OrigenInicio
    {
        public const string Manual = "manual";
        public const string Tarea = "tarea";
        public const string Instalador = "instalador";

        public static string Resolver(string[] argumentos)
        {
            if (argumentos == null)
            {
                return Manual;
            }

            foreach (string argumento in argumentos)
            {
                if (string.Equals(argumento, "--origen=tarea", StringComparison.OrdinalIgnoreCase))
                {
                    return Tarea;
                }

                if (string.Equals(argumento, "--origen=instalador", StringComparison.OrdinalIgnoreCase))
                {
                    return Instalador;
                }
            }

            return Manual;
        }
    }

    public static class RegistroOperativo
    {
        public static string Inicio(int pid, string version, string origen)
        {
            return "Inicio de la aplicacion. PID " + pid + ". Version " + version + ". Origen " + origen + ".";
        }

        public static string MutexAdquirido(int pid)
        {
            return "Mutex adquirido. PID " + pid + ".";
        }

        public static string InstanciaExistente(int pid)
        {
            return "Se detectó una instancia existente. PID " + pid + ".";
        }

        public static string Senal(int pid)
        {
            return "Senal recibida desde otra ejecucion. PID " + pid + ".";
        }

        public static string TaskbarCreada()
        {
            return "TaskbarCreated.";
        }

        public static string IconoRepublicado()
        {
            return "Icono republicado.";
        }

        public static string Fin(int pid)
        {
            return "Finalizacion del proceso. PID " + pid + ".";
        }
    }

    public sealed class GestorIconoBandeja
    {
        public int IconosCreados { get; private set; }
        public int Timers { get; private set; }
        public int Suscripciones { get; private set; }
        public int Republicaciones { get; private set; }
        public int Disposes { get; private set; }
        public bool Visible { get; private set; }

        public bool CrearIcono()
        {
            if (IconosCreados != 0 || Disposes != 0)
            {
                return false;
            }

            IconosCreados = 1;
            Visible = true;
            return true;
        }

        public void RegistrarTimer()
        {
            Timers++;
        }

        public bool SuscribirBarra()
        {
            if (Suscripciones != 0 || Disposes != 0)
            {
                return false;
            }

            Suscripciones = 1;
            return true;
        }

        public void Republicar()
        {
            if (IconosCreados != 1 || Disposes != 0)
            {
                return;
            }

            Visible = false;
            Visible = true;
            Republicaciones++;
        }

        public void Cerrar()
        {
            if (Disposes != 0)
            {
                return;
            }

            Visible = false;
            Disposes = 1;
        }
    }

    public sealed class ReintentoManual
    {
        private readonly OperacionExclusiva puerta = new OperacionExclusiva();

        public bool Iniciar(Action trabajo)
        {
            if (trabajo == null || !puerta.TryEnter())
            {
                return false;
            }

            try
            {
                System.Threading.ThreadPool.QueueUserWorkItem(_ =>
                {
                    try
                    {
                        trabajo();
                    }
                    catch (Exception)
                    {
                    }
                    finally
                    {
                        puerta.Salir();
                    }
                });
                return true;
            }
            catch (Exception)
            {
                puerta.Salir();
                return false;
            }
        }
    }

    public readonly struct ResultadoCanal
    {
        public bool Aceptado { get; }
        public string Motivo { get; }

        private ResultadoCanal(bool aceptado, string motivo)
        {
            Aceptado = aceptado;
            Motivo = motivo ?? string.Empty;
        }

        public static ResultadoCanal Ok()
        {
            return new ResultadoCanal(true, string.Empty);
        }

        public static ResultadoCanal Fallo(string motivo)
        {
            return new ResultadoCanal(false, motivo);
        }
    }

    public sealed class PoliticaIntervalo
    {
        private readonly object _sync = new object();
        private readonly bool _conservarIntervaloNormal;
        private readonly int _intervaloNormalMs;

        public int Fallos { get; private set; }
        public int IntervaloMs { get; private set; }

        public PoliticaIntervalo(int intervaloNormalMs, bool conservarIntervaloNormal)
        {
            if (intervaloNormalMs <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(intervaloNormalMs));
            }

            _intervaloNormalMs = intervaloNormalMs;
            _conservarIntervaloNormal = conservarIntervaloNormal;
            IntervaloMs = intervaloNormalMs;
        }

        public void RegistrarFalloTemporal()
        {
            lock (_sync)
            {
                Fallos++;
                int propuesto = CicloVidaAgente.IntervaloReintentoMs(Fallos);
                IntervaloMs = _conservarIntervaloNormal
                    ? Math.Max(_intervaloNormalMs, propuesto)
                    : propuesto;
            }
        }

        public void RegistrarRecuperacion()
        {
            lock (_sync)
            {
                Fallos = 0;
                IntervaloMs = _intervaloNormalMs;
            }
        }
    }

    public static class ColaTrabajoAgente
    {
        public static void Encolar(Action trabajo)
        {
            if (trabajo == null)
            {
                return;
            }

            System.Threading.ThreadPool.QueueUserWorkItem(_ => trabajo());
        }
    }

    public sealed class OperacionExclusiva
    {
        private int enCurso;

        public bool EnCurso
        {
            get { return enCurso != 0; }
        }

        public bool TryEnter()
        {
            return System.Threading.Interlocked.CompareExchange(ref enCurso, 1, 0) == 0;
        }

        public void Salir()
        {
            System.Threading.Interlocked.Exchange(ref enCurso, 0);
        }

        public void Ejecutar(Action accion, Action<Exception> alFallar)
        {
            if (!TryEnter())
            {
                return;
            }

            try
            {
                if (accion != null)
                {
                    accion();
                }
            }
            catch (Exception ex)
            {
                if (alFallar != null)
                {
                    alFallar(ex);
                }
            }
            finally
            {
                Salir();
            }
        }
    }
}
