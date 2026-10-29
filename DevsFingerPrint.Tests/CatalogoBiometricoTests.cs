using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DevsFingerPrint.Domain.Models;
using DevsFingerPrint.Infrastructure.Services;
using Xunit;

namespace DevsFingerPrint.Tests
{
    public class CatalogoBiometricoTests
    {
        [Fact]
        public void TemplateVacio_EsInvalido()
        {
            ResultadoImportacionTemplate resultado = TemplateBiometricoImporter.TryImportarTemplateBiometrico("   ");

            Assert.False(resultado.Exito);
            Assert.Equal("vacio", resultado.Motivo);
        }

        [Fact]
        public void TemplateDemasiadoCorto_EsInvalido()
        {
            ResultadoImportacionTemplate resultado = TemplateBiometricoImporter.TryImportarTemplateBiometrico("AAAA");

            Assert.False(resultado.Exito);
            Assert.Equal("demasiado-corto", resultado.Motivo);
        }

        [Fact]
        public void Base64Invalido_NoLanza()
        {
            ResultadoImportacionTemplate resultado = TemplateBiometricoImporter.TryImportarTemplateBiometrico("###no-es-base64###");

            Assert.False(resultado.Exito);
            Assert.Equal("base64-invalido", resultado.Motivo);
            Assert.DoesNotContain("###", resultado.Motivo);
        }

        [Fact]
        public void FormatoDesconocido_NoAceptaBytesArbitrarios()
        {
            string plantilla = Convert.ToBase64String(Encoding.UTF8.GetBytes(new string('x', 48)));

            ResultadoImportacionTemplate resultado = TemplateBiometricoImporter.TryImportarTemplateBiometrico(plantilla);

            Assert.False(resultado.Exito);
            Assert.Equal(FormatoTemplateBiometrico.Desconocido, resultado.Formato);
            Assert.DoesNotContain(plantilla, resultado.Motivo);
        }

        [Fact]
        public void Base64Doble_NoSeAceptaPorqueNoExisteEnElHistorial()
        {
            string interno = Convert.ToBase64String(Encoding.UTF8.GetBytes(new string('q', 40)));
            string doble = Convert.ToBase64String(Encoding.UTF8.GetBytes(interno));

            ResultadoImportacionTemplate resultado = TemplateBiometricoImporter.TryImportarTemplateBiometrico(doble);

            Assert.False(resultado.Exito);
        }

        [Theory]
        [InlineData("<FMD>historico-no-valido</FMD>")]
        [InlineData("  <Fid>historico-no-valido</Fid>  ")]
        public void XmlDirectoInvalido_NoLanzaNiExponeElTexto(string xml)
        {
            ResultadoImportacionTemplate resultado = TemplateBiometricoImporter.TryImportarTemplateBiometrico(xml);

            Assert.False(resultado.Exito);
            Assert.Equal(FormatoTemplateBiometrico.XmlHistorico, resultado.Formato);
            Assert.DoesNotContain("historico", resultado.Motivo);
        }

        [Fact]
        public void XmlEnBase64ConEspaciosYBom_SeTrataComoXmlSinExponerlo()
        {
            string xml = "<FMD>historico-no-valido</FMD>";
            string codificado = Convert.ToBase64String(Encoding.UTF8.GetBytes(xml));
            string conEspacios = "\uFEFF  " + codificado.Insert(8, " \r\n ") + "  ";

            ResultadoImportacionTemplate resultado = TemplateBiometricoImporter.TryImportarTemplateBiometrico(conEspacios);

            Assert.False(resultado.Exito);
            Assert.Equal(FormatoTemplateBiometrico.XmlHistorico, resultado.Formato);
            Assert.DoesNotContain(codificado.Substring(0, 8), resultado.Motivo);
        }

        [Fact]
        public void PlantillaAnsiOIsoSintetica_RequiereLectorParaSerValida()
        {
            byte[] sintetico = new byte[64];
            for (int i = 0; i < sintetico.Length; i++)
            {
                sintetico[i] = (byte)(i + 1);
            }

            ResultadoImportacionTemplate resultado = TemplateBiometricoImporter.TryImportarTemplateBiometrico(Convert.ToBase64String(sintetico));

            Assert.False(resultado.Exito);
            Assert.Equal("formato-desconocido", resultado.Motivo);
        }

        [Fact]
        public void HuellaInvalidaNoImpideEvaluarLaSiguiente()
        {
            var empleados = new[] { Empleado(1, true), Empleado(2, true) };
            var huellas = new[]
            {
                new Huella { EmpleadoId = 1, IndiceDedo = 1, TemplateBiometrico = "###" },
                new Huella { EmpleadoId = 2, IndiceDedo = 2, TemplateBiometrico = "" }
            };

            ResumenCatalogoBiometrico resumen = CatalogoBiometrico.Construir(empleados, huellas);

            Assert.Equal(2, resumen.HuellasRecibidas);
            Assert.Equal(0, resumen.HuellasValidas);
            Assert.Equal(2, resumen.HuellasIncompatibles);
            Assert.Empty(resumen.Compatibles);
        }

        [Fact]
        public void HuellaDeEmpleadoInexistenteOInactivo_NoQuedaDisponible()
        {
            var activos = new HashSet<int> { 1 };
            Assert.Equal("empleado", CatalogoBiometrico.ReferenciaInvalida(new Huella { EmpleadoId = 9, IndiceDedo = 1, TemplateBiometrico = "x" }, activos));
            Assert.Equal("empleado", CatalogoBiometrico.ReferenciaInvalida(new Huella { EmpleadoId = 2, IndiceDedo = 1, TemplateBiometrico = "x" }, new HashSet<int>()));
            Assert.Null(CatalogoBiometrico.ReferenciaInvalida(new Huella { EmpleadoId = 1, IndiceDedo = 1, TemplateBiometrico = "x" }, activos));
        }

        [Fact]
        public void EmpleadoInactivo_NoEntraAlCatalogoAunqueLaPlantillaExista()
        {
            var empleados = new[] { Empleado(1, false) };
            var huellas = new[] { new Huella { EmpleadoId = 1, IndiceDedo = 1, TemplateBiometrico = new string('A', 32) } };

            ResumenCatalogoBiometrico resumen = CatalogoBiometrico.Construir(empleados, huellas);

            Assert.Equal(0, resumen.EmpleadosActivos);
            Assert.Equal(0, resumen.HuellasValidas);
            Assert.Empty(resumen.Compatibles);
        }

        [Fact]
        public void SucursalDistinta_NoReemplazaYLaCoincidenteSeConserva()
        {
            var empleados = new List<Empleado>
            {
                new Empleado { Id = 1, EmpresaId = 1, SucursalId = 7, Activo = true },
                new Empleado { Id = 2, EmpresaId = 1, SucursalId = 8, Activo = true }
            };
            var huellas = new List<Huella>
            {
                new Huella { EmpleadoId = 1, IndiceDedo = 1, TemplateBiometrico = "a" },
                new Huella { EmpleadoId = 2, IndiceDedo = 1, TemplateBiometrico = "b" }
            };

            List<Empleado> destinoEmpleados;
            List<Huella> destinoHuellas;
            string motivo;
            Assert.True(CatalogoBiometrico.PrepararReemplazo(empleados, huellas, 7, out destinoEmpleados, out destinoHuellas, out motivo));
            Assert.Equal(1, destinoEmpleados.Single().Id);
            Assert.Equal(1, destinoHuellas.Single().EmpleadoId);

            Assert.False(CatalogoBiometrico.PrepararReemplazo(empleados, huellas, 99, out destinoEmpleados, out destinoHuellas, out motivo));
            Assert.Equal("catalogo-sucursal", motivo);
        }

        [Fact]
        public void HuellasSinEmpleados_NoReemplazanElCatalogo()
        {
            List<Empleado> destinoEmpleados;
            List<Huella> destinoHuellas;
            string motivo;
            bool aceptado = CatalogoBiometrico.PrepararReemplazo(
                new List<Empleado>(),
                new List<Huella> { new Huella { EmpleadoId = 4, IndiceDedo = 1, TemplateBiometrico = "x" } },
                0,
                out destinoEmpleados,
                out destinoHuellas,
                out motivo);

            Assert.False(aceptado);
            Assert.Equal("catalogo-huerfano", motivo);
        }

        [Fact]
        public void EmpresasDistintas_NoSonUnCatalogoHomogeneo()
        {
            Assert.False(CatalogoBiometrico.EmpresasHomogeneas(new[]
            {
                Empleado(1, true),
                new Empleado { Id = 2, EmpresaId = 8, Activo = true }
            }));
            Assert.True(CatalogoBiometrico.EmpresasHomogeneas(new List<Empleado>()));
        }

        [Fact]
        public void TextoDeEstadoYLog_NoIncluyenPlantillaNiCredenciales()
        {
            var resumen = new ResumenCatalogoBiometrico
            {
                EmpleadosActivos = 12,
                HuellasRecibidas = 18,
                HuellasValidas = 16,
                HuellasIncompatibles = 2,
                UltimaSincronizacionCorrecta = new DateTime(2026, 10, 26, 15, 45, 0)
            };
            string plantilla = "QUERTYTemplateBiometricoSecreto";
            string texto = CatalogoBiometrico.TextoEstado(true, "SERIE-1", resumen, 0, "Conectada");
            string log = resumen.TextoLog();

            Assert.Contains("Huellas disponibles: 16", texto);
            Assert.Contains("API: Conectada", texto);
            Assert.DoesNotContain(plantilla, texto);
            Assert.DoesNotContain("eyJ", log);
            Assert.DoesNotContain("ClientSecret", log);
        }

        [Fact]
        public void MotivoDeTimeout_NoSeConfundeConListaVacia()
        {
            Assert.Equal("timeout", ApiClient.MotivoTransporte(new System.Net.WebException("demora", System.Net.WebExceptionStatus.Timeout)));
            Assert.Equal("red", ApiClient.MotivoTransporte(new System.Net.WebException("red", System.Net.WebExceptionStatus.ConnectFailure)));
        }

        private static Empleado Empleado(int id, bool activo)
        {
            return new Empleado { Id = id, EmpresaId = 1, Activo = activo, Nombre = "N", Apellido = "A" };
        }
    }
}
