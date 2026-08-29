using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Security.Cryptography;

namespace DevsFingerPrint.Infrastructure.Services
{
    public static class CredentialStorage
    {

        private static readonly string FolderPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "ControlFichajes"
    );
        private static readonly string FilePath = Path.Combine(FolderPath, "config.dat");

        // Guarda usuario y contraseña encriptados
        public static void GuardarCredenciales(string usuario, string password)
        {
            try
            {
                if (!Directory.Exists(FolderPath))
                    Directory.CreateDirectory(FolderPath);

                string data = string.Format("{0};{1}", usuario, password);
                byte[] plainBytes = Encoding.UTF8.GetBytes(data);

                // Encripta con la clave del usuario actual de Windows
                byte[] encryptedBytes = ProtectedData.Protect(
                    plainBytes,
                    null,
                    DataProtectionScope.CurrentUser
                );

                File.WriteAllBytes(FilePath, encryptedBytes);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error al guardar credenciales: {ex.Message}");
            }
        }

        // Lee y desencripta credenciales. Devuelve (Usuario, Password) o null si no existe.
        public static string[] CargarCredenciales()
        {
            try
            {
                if (!File.Exists(FilePath))
                    return null;

                byte[] encryptedBytes = File.ReadAllBytes(FilePath);

                byte[] plainBytes = ProtectedData.Unprotect(
                    encryptedBytes,
                    null,
                    DataProtectionScope.CurrentUser
                );

                string data = Encoding.UTF8.GetString(plainBytes);
                return data.Split(';');
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error al cargar credenciales: {ex.Message}");
                return null; // Si falla la desencriptación o el archivo está corrupto
            }
        }

        // Por si en algún momento querés cerrar sesión o cambiar de usuario
        public static void BorrarCredenciales()
        {
            if (File.Exists(FilePath))
                File.Delete(FilePath);
        }


    }
}
