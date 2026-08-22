using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DevsFingerPrint.Domain.Models
{
    public class Huella
    {
        public int Id { get; set; }
        public int EmpleadoId { get; set; }
        public string NombreDedo { get; set; }
        public string TemplateBiometrico { get; set; } // Representación en Base64
    }
}
