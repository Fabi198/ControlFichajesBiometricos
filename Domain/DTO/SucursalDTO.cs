using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DevsFingerPrint.Domain.DTO
{
    public class SucursalDTO
    {

        public int Id { get; set; }
        public string Nombre { get; set; }
        public int EmpresaId { get; set; }
        public string SerialLector { get; set; }

    }
}
