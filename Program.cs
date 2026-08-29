using DevsFingerPrint.Infrastructure.Services;
using DevsFingerPrint.Presentation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

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

            ApiClient apiClient = new ApiClient("http://161.153.193.159:80");
            bool logueado = false;

            // 1. Intentar cargar credenciales guardadas en AppData
            string[] credenciales = CredentialStorage.CargarCredenciales();

            if (credenciales != null && credenciales.Length == 2)
            {
                string usuarioGuardado = credenciales[0];
                string passwordGuardado = credenciales[1];

                // Probar autenticar en silencio
                logueado = apiClient.IniciarSesion(usuarioGuardado, passwordGuardado);
            }

            // 2. Si no hay credenciales o la autenticación falló, pedir Login manual
            if (!logueado)
            {
                using (FormLogin loginForm = new FormLogin(apiClient))
                {
                    if (loginForm.ShowDialog() != DialogResult.OK)
                    {
                        // Si el usuario cerró la ventana de login sin ingresar, se cierra el programa
                        return;
                    }
                }
            }

            // 3. Iniciar la ventana principal del sistema (ya autenticado y con JWT listo)
            Application.Run(new MainTrayContext(apiClient));
        }
    }
}
