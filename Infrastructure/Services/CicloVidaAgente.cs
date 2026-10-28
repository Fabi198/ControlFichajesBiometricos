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
        public const int IntervaloHeartbeatNormalMs = 30000;
        public const int IntervaloSincronizacionNormalMs = 1200000;

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
    }

    public sealed class ReintentoConexion
    {
        public int Fallos { get; private set; }
        public int IntervaloHeartbeatMs { get; private set; }
        public int IntervaloSincronizacionMs { get; private set; }

        public ReintentoConexion()
        {
            IntervaloHeartbeatMs = CicloVidaAgente.IntervaloHeartbeatNormalMs;
            IntervaloSincronizacionMs = CicloVidaAgente.IntervaloSincronizacionNormalMs;
        }

        public void RegistrarFalloTemporal()
        {
            Fallos++;
            IntervaloHeartbeatMs = CicloVidaAgente.IntervaloReintentoMs(Fallos);
            IntervaloSincronizacionMs = Math.Max(IntervaloHeartbeatMs, 60000);
        }

        public void RegistrarRecuperacion()
        {
            Fallos = 0;
            IntervaloHeartbeatMs = CicloVidaAgente.IntervaloHeartbeatNormalMs;
            IntervaloSincronizacionMs = CicloVidaAgente.IntervaloSincronizacionNormalMs;
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
