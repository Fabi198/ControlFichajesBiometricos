using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

public static class CredentialStorage
{
    private static readonly string FolderPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ControlFichajes");
    private static readonly string FilePath = Path.Combine(FolderPath, "config.dat");

    public static void GuardarCredenciales(string clientId, string clientSecret)
    {
        if (!Directory.Exists(FolderPath))
        {
            Directory.CreateDirectory(FolderPath);
        }

        // Unimos ambos valores separados por un punto y coma
        string rawData = $"{clientId};{clientSecret}";
        byte[] plainTextBytes = Encoding.UTF8.GetBytes(rawData);

        // Cifrado nativo de Windows (DPAPI) vinculado al usuario actual de la PC
        byte[] encryptedBytes = ProtectedData.Protect(plainTextBytes, null, DataProtectionScope.CurrentUser);

        File.WriteAllBytes(FilePath, encryptedBytes);
    }

    public static bool CargarCredenciales(out string clientId, out string clientSecret)
    {
        clientId = string.Empty;
        clientSecret = string.Empty;

        if (!File.Exists(FilePath))
            return false;

        try
        {
            byte[] encryptedBytes = File.ReadAllBytes(FilePath);

            // Descifrado DPAPI
            byte[] plainTextBytes = ProtectedData.Unprotect(encryptedBytes, null, DataProtectionScope.CurrentUser);

            string rawData = Encoding.UTF8.GetString(plainTextBytes);
            string[] parts = rawData.Split(new char[] { ';' }, 2);

            if (parts.Length == 2)
            {
                clientId = parts[0];
                clientSecret = parts[1];
                return true;
            }
        }
        catch
        {
            // Si el archivo está corrupto o cambió el contexto de seguridad, limpiamos
            BorrarCredenciales();
        }

        return false;
    }

    public static void BorrarCredenciales()
    {
        if (File.Exists(FilePath))
        {
            File.Delete(FilePath);
        }
    }
}