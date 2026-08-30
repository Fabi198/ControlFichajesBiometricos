using DevsFingerPrint.Domain.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DevsFingerPrint.Domain.DTO
{
    public class CatalogoDto
    {
        public List<Empleado> Empleados { get; set; } = new List<Empleado>();
        public List<Huella> Huellas { get; set; } = new List<Huella>();
    }
}
