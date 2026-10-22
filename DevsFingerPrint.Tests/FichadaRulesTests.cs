using System;
using DevsFingerPrint.Domain.Models;
using DevsFingerPrint.Infrastructure.Services;
using Xunit;

namespace DevsFingerPrint.Tests
{
    public class FichadaRulesTests
    {
        [Theory]
        [InlineData(4, 59, true)]
        [InlineData(5, 0, false)]
        [InlineData(5, 1, false)]
        public void EsDuplicada_AplicaLimiteDeCincoMinutos(int minutos, int segundos, bool esperado)
        {
            var ahora = new DateTime(2026, 10, 12, 9, 0, 0);
            var ultimaFichada = CrearFichada(ahora.AddMinutes(-minutos).AddSeconds(-segundos));

            Assert.Equal(esperado, FichadaRules.EsDuplicada(ultimaFichada, ahora));
        }

        [Fact]
        public void EsDuplicada_ConsideraDuplicadaUnaFichadaConMarcaDeTiempoFutura()
        {
            var ahora = new DateTime(2026, 10, 12, 9, 0, 0);

            Assert.True(FichadaRules.EsDuplicada(CrearFichada(ahora.AddMinutes(1)), ahora));
        }

        [Fact]
        public void DeterminarTipoRegistro_SinFichadaAnteriorDevuelveEntrada()
        {
            Assert.Equal("Entrada", FichadaRules.DeterminarTipoRegistro(null, new DateTime(2026, 10, 12, 9, 0, 0)));
        }

        [Theory]
        [InlineData("Entrada", "Salida")]
        [InlineData("Salida", "Entrada")]
        public void DeterminarTipoRegistro_AlternaDuranteElMismoDia(string anterior, string esperado)
        {
            var ahora = new DateTime(2026, 10, 12, 18, 0, 0);
            var ultimaFichada = CrearFichada(ahora.AddHours(-8), anterior);

            Assert.Equal(esperado, FichadaRules.DeterminarTipoRegistro(ultimaFichada, ahora));
        }

        [Theory]
        [InlineData("Entrada")]
        [InlineData("Salida")]
        public void DeterminarTipoRegistro_IniciaComoEntradaAlCambiarElDia(string tipoAnterior)
        {
            var ayer = new DateTime(2026, 10, 11, 23, 59, 0);
            var hoy = new DateTime(2026, 10, 12, 0, 1, 0);

            Assert.Equal("Entrada", FichadaRules.DeterminarTipoRegistro(CrearFichada(ayer, tipoAnterior), hoy));
        }

        private static Fichada CrearFichada(DateTime fechaHora, string tipoRegistro = "Entrada")
        {
            return new Fichada
            {
                FechaHora = fechaHora,
                TipoRegistro = tipoRegistro
            };
        }
    }
}
