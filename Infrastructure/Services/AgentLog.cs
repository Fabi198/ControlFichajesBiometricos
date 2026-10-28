using System;
using System.IO;

namespace DevsFingerPrint.Infrastructure.Services
{
    internal static class AgentLog
    {
        public static void Info(string message)
        {
            Write("INFO", message);
        }

        public static void Error(string message)
        {
            Write("ERROR", message);
        }

        public static string CarpetaLogs()
        {
            return Directorio();
        }

        public static string RutaArchivoHoy()
        {
            return Path.Combine(CarpetaLogs(), "agente-" + DateTime.Now.ToString("yyyy-MM-dd") + ".log");
        }

        public static string DescribirApi(string baseUrl)
        {
            if (Uri.TryCreate(baseUrl, UriKind.Absolute, out Uri uri))
            {
                return uri.Scheme + "://" + uri.Host;
            }

            return "url-no-valida";
        }

        private static void Write(string level, string message)
        {
            try
            {
                string directory = Directorio();
                Directory.CreateDirectory(directory);
                string line = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " " + level + " " + message + Environment.NewLine;
                File.AppendAllText(Path.Combine(directory, "agente-" + DateTime.Now.ToString("yyyy-MM-dd") + ".log"), line);
            }
            catch
            {
            }
        }

        private static string Directorio()
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "ControlFichajes",
                "Logs");
        }
    }
}
