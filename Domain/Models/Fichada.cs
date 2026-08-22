using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DevsFingerPrint.Domain.Models
{
    public class Fichada
    {
        public int Id { get; set; }
        public int EmpleadoId { get; set; }
        public DateTime FechaHora { get; set; }
        public string TipoRegistro { get; set; }
        public string Metodo { get; set; } = "Biometrico";
        public bool Sincronizado { get; set; } = false;
    }
}
