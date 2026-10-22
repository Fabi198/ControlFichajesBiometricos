using System;
using DevsFingerPrint.Domain.Models;

namespace DevsFingerPrint.Infrastructure.Services
{
    public static class FichadaRules
    {
        private static readonly TimeSpan IntervaloDuplicidad = TimeSpan.FromMinutes(5);

        public static bool EsDuplicada(Fichada ultimaFichada, DateTime fechaHora)
        {
            return ultimaFichada != null &&
                   fechaHora - ultimaFichada.FechaHora < IntervaloDuplicidad;
        }

        public static string DeterminarTipoRegistro(Fichada ultimaFichada, DateTime fechaHora)
        {
            if (ultimaFichada == null || ultimaFichada.FechaHora.Date < fechaHora.Date)
            {
                return "Entrada";
            }

            return ultimaFichada.TipoRegistro == "Entrada" ? "Salida" : "Entrada";
        }
    }
}
