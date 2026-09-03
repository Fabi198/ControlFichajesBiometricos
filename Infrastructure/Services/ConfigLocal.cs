using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Web.Script.Serialization;

namespace DevsFingerPrint.Infrastructure.Services
{
    public class ConfiguracionLocalDto
    {
        public int SucursalId { get; set; }
        public string SerialLector { get; set; }
    }

    public static class DispositivoConfigService
    {
        private static readonly string RutaCarpeta = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DevsFingerPrint");
        private static readonly string RutaArchivo = Path.Combine(RutaCarpeta, "config.dat");

        public static void GuardarConfiguracion(int sucursalId, string serialLector)
        {
            try
            {
                if (!Directory.Exists(RutaCarpeta)) Directory.CreateDirectory(RutaCarpeta);

                var config = new ConfiguracionLocalDto
                {
                    SucursalId = sucursalId,
                    SerialLector = serialLector
                };

                var serializer = new JavaScriptSerializer();
                string jsonString = serializer.Serialize(config);

                byte[] bytesPlainText = Encoding.UTF8.GetBytes(jsonString);
                byte[] bytesEncrypted = ProtectedData.Protect(bytesPlainText, null, DataProtectionScope.CurrentUser);

                File.WriteAllBytes(RutaArchivo, bytesEncrypted);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[LOG Error Config] " + ex.Message);
            }
        }

        public static ConfiguracionLocalDto LeerConfiguracion()
        {
            try
            {
                if (!File.Exists(RutaArchivo)) return null;

                byte[] bytesEncrypted = File.ReadAllBytes(RutaArchivo);
                byte[] bytesPlainText = ProtectedData.Unprotect(bytesEncrypted, null, DataProtectionScope.CurrentUser);

                string jsonString = Encoding.UTF8.GetString(bytesPlainText);
                var serializer = new JavaScriptSerializer();
                return serializer.Deserialize<ConfiguracionLocalDto>(jsonString);
            }
            catch
            {
                return null;
            }
        }
    }
}
