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
        private static readonly string RutaArchivo = Path.Combine(RutaCarpeta, "device-config.dat");
        private static readonly string RutaArchivoAnterior = Path.Combine(RutaCarpeta, "config.dat");

        public static void GuardarConfiguracion(int sucursalId, string serialLector)
        {
            GuardarConfiguracionEn(RutaCarpeta, sucursalId, serialLector);
        }

        internal static void GuardarConfiguracionEn(string folderPath, int sucursalId, string serialLector)
        {
            try
            {
                Directory.CreateDirectory(folderPath);

                var config = new ConfiguracionLocalDto
                {
                    SucursalId = sucursalId,
                    SerialLector = serialLector
                };

                var serializer = new JavaScriptSerializer();
                string jsonString = serializer.Serialize(config);

                byte[] bytesPlainText = Encoding.UTF8.GetBytes(jsonString);
                byte[] bytesEncrypted = ProtectedData.Protect(bytesPlainText, null, DataProtectionScope.CurrentUser);

                File.WriteAllBytes(Path.Combine(folderPath, Path.GetFileName(RutaArchivo)), bytesEncrypted);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[LOG Error Config] " + ex.Message);
            }
        }

        public static ConfiguracionLocalDto LeerConfiguracion()
        {
            return LeerConfiguracionDesde(RutaCarpeta);
        }

        internal static ConfiguracionLocalDto LeerConfiguracionDesde(string folderPath)
        {
            try
            {
                string rutaActual = Path.Combine(folderPath, Path.GetFileName(RutaArchivo));
                string rutaAnterior = Path.Combine(folderPath, Path.GetFileName(RutaArchivoAnterior));
                string ruta = File.Exists(rutaActual) ? rutaActual : rutaAnterior;
                if (!File.Exists(ruta)) return null;

                byte[] bytesEncrypted = File.ReadAllBytes(ruta);
                byte[] bytesPlainText = ProtectedData.Unprotect(bytesEncrypted, null, DataProtectionScope.CurrentUser);

                string jsonString = Encoding.UTF8.GetString(bytesPlainText);
                var serializer = new JavaScriptSerializer();
                var configuracion = serializer.Deserialize<ConfiguracionLocalDto>(jsonString);
                if (configuracion == null ||
                    (configuracion.SucursalId <= 0 && string.IsNullOrWhiteSpace(configuracion.SerialLector)))
                {
                    return null;
                }

                if (ruta == rutaAnterior)
                {
                    GuardarConfiguracionEn(folderPath, configuracion.SucursalId, configuracion.SerialLector);
                }

                return configuracion;
            }
            catch (Exception ex) when (
                ex is CryptographicException ||
                ex is IOException ||
                ex is UnauthorizedAccessException ||
                ex is ArgumentException)
            {
                System.Diagnostics.Debug.WriteLine("[LOG Configuración local] No se pudo leer la configuración del dispositivo: " + ex.Message);
                return null;
            }
        }
    }
}
