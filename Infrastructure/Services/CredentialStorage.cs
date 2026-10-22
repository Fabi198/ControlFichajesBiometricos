using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

public static class CredentialStorage
{
    private static readonly string FolderPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DevsFingerPrint");
    private static readonly string FilePath = Path.Combine(FolderPath, "agent-credentials.dat");
    private static readonly string LegacyFilePath = Path.Combine(FolderPath, "config.dat");

    public static void GuardarCredenciales(string clientId, string clientSecret)
    {
        GuardarCredencialesEn(FolderPath, clientId, clientSecret);
    }

    internal static void GuardarCredencialesEn(string folderPath, string clientId, string clientSecret)
    {
        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
        {
            throw new ArgumentException("El identificador y el secreto del agente son obligatorios.");
        }

        Directory.CreateDirectory(folderPath);
        string rawData = $"{clientId};{clientSecret}";
        byte[] plainTextBytes = Encoding.UTF8.GetBytes(rawData);

        byte[] encryptedBytes = ProtectedData.Protect(plainTextBytes, null, DataProtectionScope.CurrentUser);
        string credentialsPath = Path.Combine(folderPath, Path.GetFileName(FilePath));
        string temporaryPath = credentialsPath + ".tmp";
        File.WriteAllBytes(temporaryPath, encryptedBytes);
        if (File.Exists(credentialsPath))
        {
            File.Replace(temporaryPath, credentialsPath, null);
        }
        else
        {
            File.Move(temporaryPath, credentialsPath);
        }
    }

    public static bool CargarCredenciales(out string clientId, out string clientSecret)
    {
        return CargarCredencialesDesde(FolderPath, out clientId, out clientSecret);
    }

    internal static bool CargarCredencialesDesde(string folderPath, out string clientId, out string clientSecret)
    {
        clientId = string.Empty;
        clientSecret = string.Empty;
        string credentialsPath = Path.Combine(folderPath, Path.GetFileName(FilePath));
        string legacyPath = Path.Combine(folderPath, Path.GetFileName(LegacyFilePath));

        if (File.Exists(credentialsPath) && TryLoadCredentials(credentialsPath, out clientId, out clientSecret))
        {
            return true;
        }

        if (!File.Exists(legacyPath) || !TryLoadCredentials(legacyPath, out clientId, out clientSecret))
        {
            return false;
        }

        GuardarCredencialesEn(folderPath, clientId, clientSecret);
        return true;
    }

    public static void BorrarCredenciales()
    {
        BorrarCredencialesEn(FolderPath);
    }

    internal static void BorrarCredencialesEn(string folderPath)
    {
        string credentialsPath = Path.Combine(folderPath, Path.GetFileName(FilePath));
        if (File.Exists(credentialsPath))
        {
            File.Delete(credentialsPath);
        }
    }

    private static bool TryLoadCredentials(string path, out string clientId, out string clientSecret)
    {
        clientId = string.Empty;
        clientSecret = string.Empty;

        try
        {
            byte[] encryptedBytes = File.ReadAllBytes(path);
            byte[] plainTextBytes = ProtectedData.Unprotect(encryptedBytes, null, DataProtectionScope.CurrentUser);
            string rawData = Encoding.UTF8.GetString(plainTextBytes);
            string[] parts = rawData.Split(new[] { ';' }, 2);
            if (parts.Length != 2 || string.IsNullOrWhiteSpace(parts[0]) || string.IsNullOrWhiteSpace(parts[1]))
            {
                return false;
            }

            clientId = parts[0];
            clientSecret = parts[1];
            return true;
        }
        catch (Exception ex) when (
            ex is CryptographicException ||
            ex is IOException ||
            ex is UnauthorizedAccessException ||
            ex is ArgumentException)
        {
            System.Diagnostics.Debug.WriteLine("[LOG Credenciales] No se pudieron leer las credenciales locales: " + ex.Message);
            return false;
        }
    }
}