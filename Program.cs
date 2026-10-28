using DevsFingerPrint.Infrastructure.Services;
using DevsFingerPrint.Presentation;
using System;
using System.Configuration;
using System.Reflection;
using System.Windows.Forms;

namespace DevsFingerPrint
{
    internal static class Program
    {
        private const string ApiProduccion = "https://controldeaccesos.online";

        /// <summary>
        /// Punto de entrada principal para la aplicación.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += (sender, args) => MostrarFalloInesperado(args.Exception);
            AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
            {
                Exception ex = args.ExceptionObject as Exception;
                AgentLog.Error(ex == null
                    ? CicloVidaAgente.TextoCierre(MotivoCierre.ExcepcionNoControlada)
                    : CicloVidaAgente.TextoCierre(MotivoCierre.ExcepcionNoControlada) + " " + ex.GetType().Name + ".");
            };

            string version = Assembly.GetExecutingAssembly().GetName().Version.ToString();
            AgentLog.Info("Inicio de la aplicacion. Version " + version);

            string apiUrl = ResolverApiBaseUrl();
            AgentLog.Info("API " + AgentLog.DescribirApi(apiUrl) + ".");

            try
            {
                ApiClient apiClient = new ApiClient(apiUrl);
                bool logueado = false;
                string clientId;
                string clientSecret;

                if (CredentialStorage.CargarCredenciales(out clientId, out clientSecret))
                {
                    logueado = apiClient.IniciarSesionAgente(clientId, clientSecret);
                }
                else
                {
                    AgentLog.Info("No hay credenciales locales. Se abre el formulario de acceso.");
                }

                if (!logueado)
                {
                    using (FormLogin formInstalacion = new FormLogin(apiClient))
                    {
                        bool cancelado = formInstalacion.ShowDialog() != DialogResult.OK;
                        logueado = !cancelado;
                        if (CicloVidaAgente.DebeTerminarProceso(logueado, cancelado))
                        {
                            return;
                        }
                    }
                }

                if (!logueado)
                {
                    AgentLog.Info("Inicio sin sesion. El agente permanece activo.");
                }

                try
                {
                    Application.Run(new MainTrayContext(apiClient));
                }
                catch (Exception ex)
                {
                    AgentLog.Error("Fallo al iniciar la bandeja, la base local o una dependencia nativa: " + ex.GetType().Name + ": " + ex.Message);
                    MessageBox.Show(
                        "No se pudo iniciar el agente. El detalle quedó en " + AgentLog.RutaArchivoHoy() + ".",
                        "Control de Accesos",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                }
            }
            catch (Exception ex)
            {
                AgentLog.Error("Fallo de inicio: " + ex.GetType().Name + ": " + ex.Message);
                MessageBox.Show(
                    "No se pudo iniciar el agente. El detalle quedó en " + AgentLog.RutaArchivoHoy() + ".",
                    "Control de Accesos",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        internal static string ResolverApiBaseUrl()
        {
            string configurada = ConfigurationManager.AppSettings["ApiBaseUrl"];
            if (string.IsNullOrWhiteSpace(configurada))
            {
                configurada = ApiProduccion;
            }

            return configurada.Trim().TrimEnd('/');
        }

        private static void MostrarFalloInesperado(Exception ex)
        {
            AgentLog.Error(CicloVidaAgente.TextoCierre(MotivoCierre.ExcepcionNoControlada) + " " + ex.GetType().Name + ".");
            try
            {
                MessageBox.Show(
                    "Ocurrió un error inesperado. El detalle quedó en " + AgentLog.RutaArchivoHoy() + ".",
                    "Control de Accesos",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
            catch
            {
            }
        }
    }
}
