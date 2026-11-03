using System;
using System.Reflection;
using DevsFingerPrint.Infrastructure.Services;
using Xunit;

namespace DevsFingerPrint.Tests
{
    public class SeleccionLectorTests
    {
        [Fact]
        public void PorSerial_EncuentraElMismoLectorSinDistinguirMayusculas()
        {
            var lectores = new[]
            {
                new { Serial = "OTRO-LECTOR" },
                new { Serial = "Lector-Guardado" }
            };

            var seleccionado = SeleccionLector.PorSerial(lectores, "lector-guardado", lector => lector.Serial);

            Assert.Same(lectores[1], seleccionado);
        }

        [Fact]
        public void PorSerial_NoUsaOtroLectorCuandoElGuardadoNoEstaConectado()
        {
            var lectores = new[]
            {
                new { Serial = "OTRO-LECTOR" }
            };

            var seleccionado = SeleccionLector.PorSerial(lectores, "LECTOR-GUARDADO", lector => lector.Serial);

            Assert.Null(seleccionado);
        }

        [Fact]
        public void PorSerial_SinSerialGuardadoUsaElPrimerLectorDisponible()
        {
            var lectores = new[]
            {
                new { Serial = "PRIMER-LECTOR" },
                new { Serial = "SEGUNDO-LECTOR" }
            };

            var seleccionado = SeleccionLector.PorSerial(lectores, null, lector => lector.Serial);

            Assert.Same(lectores[0], seleccionado);
        }

        [Fact]
        public void PorSerial_RechazaUnSelectorNulo()
        {
            Assert.Throws<ArgumentNullException>(() =>
                SeleccionLector.PorSerial(new[] { "LECTOR" }, "LECTOR", null));
        }

        [Fact]
        public void EstadoLector_DesconectaYSeRecuperaSegunElEstadoDelDispositivo()
        {
            Assert.False(InterpretarLectorDisponible(
                "La captura del lector falló: DP_DEVICE_FAILURE", true));
            Assert.True(InterpretarLectorDisponible(
                "Lector conectado: U.are.U 4500", false));
            Assert.True(InterpretarLectorDisponible(
                "Enrolamiento listo. Coloque el mismo dedo en el lector.", true));
        }

        private static bool InterpretarLectorDisponible(string mensaje, bool estadoActual)
        {
            Type mainTrayContext = typeof(ApiClient).Assembly.GetType("DevsFingerPrint.Presentation.MainTrayContext");
            MethodInfo interpretar = mainTrayContext.GetMethod(
                "InterpretarLectorDisponible",
                BindingFlags.NonPublic | BindingFlags.Static);
            return (bool)interpretar.Invoke(null, new object[] { mensaje, estadoActual });
        }
    }
}
