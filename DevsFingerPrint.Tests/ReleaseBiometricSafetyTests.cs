using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using DevsFingerPrint.Domain.Models;
using Xunit;

namespace DevsFingerPrint.Tests
{
    public class ReleaseBiometricSafetyTests
    {
        [Fact]
        public void ApplicationConfig_RedirectsResourceExtensionToBundledAssemblyVersion()
        {
            var application = typeof(Infrastructure.Services.ApiClient).Assembly;
            var applicationPath = new Uri(application.CodeBase).LocalPath;
            var applicationDirectory = Path.GetDirectoryName(applicationPath);
            var config = System.Xml.Linq.XDocument.Load(applicationPath + ".config");
            var assemblyBindingNamespace = (System.Xml.Linq.XNamespace)"urn:schemas-microsoft-com:asm.v1";
            var redirect = config.Root
                .Element("runtime")
                .Element(assemblyBindingNamespace + "assemblyBinding")
                .Elements(assemblyBindingNamespace + "dependentAssembly")
                .Single(element =>
                    (string)element.Element(assemblyBindingNamespace + "assemblyIdentity").Attribute("name") ==
                    "System.Resources.Extensions")
                .Element(assemblyBindingNamespace + "bindingRedirect");
            var bundledAssembly = AssemblyName.GetAssemblyName(
                Path.Combine(applicationDirectory, "System.Resources.Extensions.dll"));

            Assert.Equal(bundledAssembly.Version.ToString(), (string)redirect.Attribute("newVersion"));
        }

#if !DEBUG
        [Fact]
        public void ReleaseBuild_UsesPhysicalReaderAndExcludesMockAndSimulation()
        {
            var application = typeof(Infrastructure.Services.ApiClient).Assembly;
            var physicalServiceType = application.GetType("DevsFingerPrint.Infrastructure.Services.DigitalPersonaBiometricService");
            var trayType = application.GetType("DevsFingerPrint.Presentation.MainTrayContext");

            Assert.Null(application.GetType("DevsFingerPrint.Infrastructure.Services.BiometricService"));
            Assert.Null(application.GetType("DevsFingerPrint.Infrastructure.Services.ReleaseBiometricService"));
            Assert.NotNull(physicalServiceType);
            Assert.NotNull(trayType);
            Assert.NotNull(trayType.GetMethod("EnrolarNuevoEmpleado", BindingFlags.Instance | BindingFlags.NonPublic));
            Assert.Null(trayType.GetMethod("ConstruirMenuEmpleadosSimulacion", BindingFlags.Instance | BindingFlags.NonPublic));
            Assert.Null(trayType.GetMethod("SimularLecturaDedo", BindingFlags.Instance | BindingFlags.NonPublic));
            Assert.NotNull(physicalServiceType.GetMethod("IniciarLectura"));
            Assert.NotNull(physicalServiceType.GetMethod("IdentificarEmpleado"));
        }
#endif
    }
}
