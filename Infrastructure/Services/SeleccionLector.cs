using System;
using System.Collections.Generic;
using System.Linq;

namespace DevsFingerPrint.Infrastructure.Services
{
    internal static class SeleccionLector
    {
        internal static T PorSerial<T>(
            IEnumerable<T> lectores,
            string serialGuardado,
            Func<T, string> obtenerSerial)
        {
            if (lectores == null)
            {
                throw new ArgumentNullException(nameof(lectores));
            }

            if (obtenerSerial == null)
            {
                throw new ArgumentNullException(nameof(obtenerSerial));
            }

            if (string.IsNullOrWhiteSpace(serialGuardado))
            {
                return lectores.FirstOrDefault();
            }

            return lectores.FirstOrDefault(lector =>
                string.Equals(obtenerSerial(lector), serialGuardado, StringComparison.OrdinalIgnoreCase));
        }
    }
}
