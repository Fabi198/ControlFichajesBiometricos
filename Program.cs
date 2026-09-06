using DevsFingerPrint.Infrastructure.Services;
using DevsFingerPrint.Presentation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using System.Configuration;

namespace DevsFingerPrint
{
    internal static class Program
    {
        /// <summary>
        /// Punto de entrada principal para la aplicación.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            string apiUrl = ConfigurationManager.AppSettings["ApiBaseUrl"] ?? "http://161.153.193.159:80";

            ApiClient apiClient = new ApiClient(apiUrl);
            bool logueado = false;
            string clientId, clientSecret;

            // 2. Intentar cargar credenciales de agente guardadas localmente (Paso 2)
            if (CredentialStorage.CargarCredenciales(out clientId, out clientSecret))
            {
                // 3. Intentar loguear automáticamente en la API (Paso 3)
                logueado = apiClient.IniciarSesionAgente(clientId, clientSecret);
            }

            // 4. Si no hay credenciales válidas, mostrar el formulario de alta/instalación
            if (!logueado)
            {
                FormLogin formInstalacion = new FormLogin(apiClient);
                if (formInstalacion.ShowDialog() != DialogResult.OK)
                {
                    return; // Si el usuario cancela, se cierra la app
                }
            }

            // 5. Arrancar la aplicación con el contexto del agente activo
            Application.Run(new MainTrayContext(apiClient));
        }
    }
}
