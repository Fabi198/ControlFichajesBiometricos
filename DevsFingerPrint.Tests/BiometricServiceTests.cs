using System;
using System.Collections.Generic;
using DevsFingerPrint.Domain.Interfaces;
using DevsFingerPrint.Domain.Models;
using DevsFingerPrint.Infrastructure.Services;
using Xunit;

namespace DevsFingerPrint.Tests
{
#if DEBUG
    public class BiometricServiceTests
    {
        [Fact]
        public void Enrolamiento_RequiereCuatroCapturasYPublicaProgreso()
        {
            var service = CrearServicio();
            var progress = new List<int>();
            ((IBiometricService)service).OnProgresoEnrolamiento += progress.Add;

            service.IniciarEnrolamiento();
            Assert.Null(service.ObtenerTemplateBase64Resultado());

            for (int sample = 0; sample < 4; sample++)
            {
                service.SimularLecturaDedo(4);
            }

            var template = service.ObtenerTemplateBase64Resultado();

            Assert.Equal(new[] { 0, 1, 2, 3, 4 }, progress);
            Assert.Equal(200, Convert.FromBase64String(template).Length);
        }

        [Fact]
        public void CancelarEnrolamiento_DescartaMuestrasParciales()
        {
            var service = CrearServicio();
            var identifiedEmployees = new List<int>();
            service.OnEmpleadoIdentificado += identifiedEmployees.Add;

            service.IniciarEnrolamiento();
            service.SimularLecturaDedo(11);
            service.SimularLecturaDedo(11);
            service.CancelarEnrolamiento();

            Assert.Null(service.ObtenerTemplateBase64Resultado());
            service.SimularLecturaDedo(12);
            Assert.Equal(new[] { 12 }, identifiedEmployees);
        }

        [Fact]
        public void IdentificacionSinHuellas_DisparaEventoDeNoReconocida()
        {
            var service = CrearServicio();
            int noRecognized = 0;
            service.OnHuellaNoReconocida += () => noRecognized++;

            var identifyMethod = typeof(BiometricService).GetMethod("IdentificarEmpleado");
            Type tipoHuella = typeof(BiometricService).Assembly.GetType("DevsFingerPrint.Domain.Models.HuellaEnMemoria");
            Array sinHuellas = Array.CreateInstance(tipoHuella, 0);
            object empleadoId = identifyMethod.Invoke(service, new object[] { null, sinHuellas });

            Assert.Null(empleadoId);
            Assert.Equal(1, noRecognized);
        }

        [Fact]
        public void CapturaSimulada_EmiteElEmpleadoConfigurado()
        {
            var service = CrearServicio();
            var identifiedEmployees = new List<int>();
            service.OnEmpleadoIdentificado += identifiedEmployees.Add;

            service.SimularLecturaDedo(42);

            Assert.Equal(new[] { 42 }, identifiedEmployees);
        }

        private static BiometricService CrearServicio()
        {
            return new BiometricService(new ApiClient("http://127.0.0.1"));
        }
    }
#endif
}
