using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using DevsFingerPrint.Infrastructure.Services;
using Xunit;

namespace DevsFingerPrint.Tests
{
    public class UserConfigurationStorageTests
    {
        [Fact]
        public void DeviceConfigurationDoesNotOverwriteAgentCredentials()
        {
            string folder = CreateTemporaryFolder();
            try
            {
                CredentialStorage.GuardarCredencialesEn(folder, "test-agent", "test-secret");
                DispositivoConfigService.GuardarConfiguracionEn(folder, 23, "DP-TEST-SERIAL");

                Assert.True(CredentialStorage.CargarCredencialesDesde(folder, out string clientId, out string secret));
                Assert.Equal("test-agent", clientId);
                Assert.Equal("test-secret", secret);
                Assert.Equal(23, DispositivoConfigService.LeerConfiguracionDesde(folder).SucursalId);
                Assert.True(File.Exists(Path.Combine(folder, "agent-credentials.dat")));
                Assert.True(File.Exists(Path.Combine(folder, "device-config.dat")));
            }
            finally
            {
                Directory.Delete(folder, recursive: true);
            }
        }

        [Fact]
        public void LegacyCredentialFileIsMigratedWithoutDeletingItsOriginal()
        {
            string folder = CreateTemporaryFolder();
            try
            {
                byte[] legacyBytes = Protect("legacy-agent;legacy-secret");
                File.WriteAllBytes(Path.Combine(folder, "config.dat"), legacyBytes);

                Assert.True(CredentialStorage.CargarCredencialesDesde(folder, out string clientId, out string secret));

                Assert.Equal("legacy-agent", clientId);
                Assert.Equal("legacy-secret", secret);
                Assert.True(File.Exists(Path.Combine(folder, "agent-credentials.dat")));
                Assert.Equal(legacyBytes, File.ReadAllBytes(Path.Combine(folder, "config.dat")));
            }
            finally
            {
                Directory.Delete(folder, recursive: true);
            }
        }

        [Fact]
        public void LegacyDeviceConfigurationIsMigratedAndPreserved()
        {
            string folder = CreateTemporaryFolder();
            try
            {
                byte[] legacyBytes = Protect("{\"SucursalId\":23,\"SerialLector\":\"DP-TEST-SERIAL\"}");
                File.WriteAllBytes(Path.Combine(folder, "config.dat"), legacyBytes);

                var configuration = DispositivoConfigService.LeerConfiguracionDesde(folder);

                Assert.NotNull(configuration);
                Assert.Equal(23, configuration.SucursalId);
                Assert.Equal("DP-TEST-SERIAL", configuration.SerialLector);
                Assert.True(File.Exists(Path.Combine(folder, "device-config.dat")));
                Assert.Equal(legacyBytes, File.ReadAllBytes(Path.Combine(folder, "config.dat")));

                CredentialStorage.GuardarCredencialesEn(folder, "test-agent", "test-secret");
                Assert.True(CredentialStorage.CargarCredencialesDesde(folder, out string clientId, out string secret));
                Assert.Equal("test-agent", clientId);
                Assert.Equal("test-secret", secret);
            }
            finally
            {
                Directory.Delete(folder, recursive: true);
            }
        }

        [Fact]
        public void DeletingCredentialsDoesNotDeleteDeviceConfigurationOrLegacyData()
        {
            string folder = CreateTemporaryFolder();
            try
            {
                CredentialStorage.GuardarCredencialesEn(folder, "test-agent", "test-secret");
                DispositivoConfigService.GuardarConfiguracionEn(folder, 23, "DP-TEST-SERIAL");
                byte[] legacyBytes = Protect("{\"SucursalId\":17,\"SerialLector\":\"OLD-SERIAL\"}");
                File.WriteAllBytes(Path.Combine(folder, "config.dat"), legacyBytes);

                CredentialStorage.BorrarCredencialesEn(folder);

                Assert.False(File.Exists(Path.Combine(folder, "agent-credentials.dat")));
                Assert.True(File.Exists(Path.Combine(folder, "device-config.dat")));
                Assert.Equal(legacyBytes, File.ReadAllBytes(Path.Combine(folder, "config.dat")));
            }
            finally
            {
                Directory.Delete(folder, recursive: true);
            }
        }

        private static byte[] Protect(string value)
        {
            return ProtectedData.Protect(
                Encoding.UTF8.GetBytes(value),
                null,
                DataProtectionScope.CurrentUser);
        }

        private static string CreateTemporaryFolder()
        {
            string folder = Path.Combine(Path.GetTempPath(), "DevsFingerPrint.Tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            return folder;
        }
    }
}
