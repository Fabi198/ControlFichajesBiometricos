using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

namespace DevsFingerPrint.Infrastructure.Services
{
    /// <summary>
    /// Una instancia por sesión interactiva. El prefijo Local evita cruzar sesiones de otros usuarios.
    /// La etiqueta de integridad baja permite que se vean el proceso elevado de la tarea y el proceso manual.
    /// </summary>
    public sealed class InstanciaUnicaAgente : IDisposable
    {
        public const string NombreProduccion = @"Local\DevsFingerPrint.ControlDeAccesos";
        private const string Sddl = "D:(A;;GA;;;WD)S:(ML;;NW;;;LW)";
        private const uint WaitObject = 0;
        private const uint WaitAbandoned = 0x00000080;
        private const int ErrorAccessDenied = 5;

        private readonly List<Action> acciones = new List<Action>();
        private readonly object puertaAcciones = new object();
        private IntPtr mutex;
        private IntPtr evento;
        private Thread hiloEscucha;
        private int hiloDueno;
        private int pendiente;
        private int dispuesto;
        private volatile bool escuchar;

        private InstanciaUnicaAgente()
        {
        }

        public bool EsPropietaria { get; private set; }

        public static InstanciaUnicaAgente Adquirir()
        {
            return Adquirir(NombreProduccion);
        }

        public static InstanciaUnicaAgente Adquirir(string nombre)
        {
            if (string.IsNullOrWhiteSpace(nombre) || nombre.IndexOf(@"Local\", StringComparison.Ordinal) != 0)
            {
                throw new ArgumentException("El mutex debe vivir en la sesión local.", "nombre");
            }

            var instancia = new InstanciaUnicaAgente();
            instancia.mutex = CrearNucleo(false, nombre);
            if (instancia.mutex == IntPtr.Zero)
            {
                instancia.EsPropietaria = false;
                instancia.evento = CrearNucleo(true, nombre + ".Senal");
                return instancia;
            }

            uint espera = WaitForSingleObject(instancia.mutex, 0);
            instancia.EsPropietaria = espera == WaitObject || espera == WaitAbandoned;
            instancia.hiloDueno = Thread.CurrentThread.ManagedThreadId;
            if (!instancia.EsPropietaria)
            {
                CloseHandle(instancia.mutex);
                instancia.mutex = IntPtr.Zero;
            }

            instancia.evento = CrearNucleo(true, nombre + ".Senal");
            return instancia;
        }

        public static bool ResolverArranque(bool esPropietaria, Action senalar)
        {
            if (esPropietaria)
            {
                return true;
            }

            if (senalar != null)
            {
                senalar();
            }

            AgentLog.Info(RegistroOperativo.InstanciaExistente(Process.GetCurrentProcess().Id));
            return false;
        }

        public void Senalar()
        {
            if (evento != IntPtr.Zero)
            {
                SetEvent(evento);
            }
        }

        public void IniciarEscucha()
        {
            if (!EsPropietaria || hiloEscucha != null || evento == IntPtr.Zero)
            {
                return;
            }

            escuchar = true;
            hiloEscucha = new Thread(BucleSenal);
            hiloEscucha.IsBackground = true;
            hiloEscucha.Name = "SenalInstanciaAgente";
            hiloEscucha.Start();
        }

        public void Escuchar(Action accion)
        {
            if (accion == null)
            {
                return;
            }

            bool hayPendiente;
            lock (puertaAcciones)
            {
                acciones.Add(accion);
                hayPendiente = pendiente == 1;
                pendiente = 0;
            }

            if (hayPendiente)
            {
                try
                {
                    accion();
                }
                catch (Exception)
                {
                }
            }
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref dispuesto, 1) != 0)
            {
                return;
            }

            escuchar = false;
            if (hiloEscucha != null && hiloEscucha != Thread.CurrentThread)
            {
                hiloEscucha.Join(1000);
            }

            if (EsPropietaria && mutex != IntPtr.Zero && Thread.CurrentThread.ManagedThreadId == hiloDueno)
            {
                ReleaseMutex(mutex);
            }

            if (mutex != IntPtr.Zero)
            {
                CloseHandle(mutex);
                mutex = IntPtr.Zero;
            }

            if (evento != IntPtr.Zero)
            {
                CloseHandle(evento);
                evento = IntPtr.Zero;
            }
        }

        private void BucleSenal()
        {
            while (escuchar && evento != IntPtr.Zero)
            {
                uint espera = WaitForSingleObject(evento, 200);
                if (!escuchar || espera != WaitObject)
                {
                    continue;
                }

                PublicarSenal();
            }
        }

        private void PublicarSenal()
        {
            AgentLog.Info(RegistroOperativo.Senal(Process.GetCurrentProcess().Id));
            Action[] copia;
            lock (puertaAcciones)
            {
                if (acciones.Count == 0)
                {
                    pendiente = 1;
                    return;
                }

                copia = acciones.ToArray();
            }

            foreach (Action accion in copia)
            {
                try
                {
                    accion();
                }
                catch (Exception)
                {
                }
            }
        }

        private static IntPtr CrearNucleo(bool esEvento, string nombre)
        {
            IntPtr descriptor = IntPtr.Zero;
            if (!ConvertStringSecurityDescriptorToSecurityDescriptor(Sddl, 1, out descriptor, IntPtr.Zero))
            {
                return IntPtr.Zero;
            }

            try
            {
                SECURITY_ATTRIBUTES atributos = new SECURITY_ATTRIBUTES();
                atributos.nLength = Marshal.SizeOf(typeof(SECURITY_ATTRIBUTES));
                atributos.lpSecurityDescriptor = descriptor;
                atributos.bInheritHandle = 0;
                IntPtr handle = esEvento
                    ? CreateEvent(ref atributos, false, false, nombre)
                    : CreateMutex(ref atributos, false, nombre);
                if (handle == IntPtr.Zero && Marshal.GetLastWin32Error() == ErrorAccessDenied)
                {
                    return IntPtr.Zero;
                }

                return handle;
            }
            finally
            {
                if (descriptor != IntPtr.Zero)
                {
                    LocalFree(descriptor);
                }
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct SECURITY_ATTRIBUTES
        {
            public int nLength;
            public IntPtr lpSecurityDescriptor;
            public int bInheritHandle;
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr CreateMutex(ref SECURITY_ATTRIBUTES atributos, bool propietarioInicial, string nombre);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr CreateEvent(ref SECURITY_ATTRIBUTES atributos, bool manual, bool estadoInicial, string nombre);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern uint WaitForSingleObject(IntPtr handle, uint milisegundos);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool ReleaseMutex(IntPtr handle);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetEvent(IntPtr handle);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr handle);

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool ConvertStringSecurityDescriptorToSecurityDescriptor(
            string descriptor,
            uint revision,
            out IntPtr securityDescriptor,
            IntPtr tamanio);

        [DllImport("kernel32.dll")]
        private static extern IntPtr LocalFree(IntPtr handle);
    }
}
