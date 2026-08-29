using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DevsFingerPrint.Domain.Models
{
    public class Empleado
    {
        public int Id { get; set; }
        public int EmpresaId { get; set; }
        public string Legajo { get; set; }
        public string DNI { get; set; }
        public string CUIL { get; set; }
        public string Nombre { get; set; }
        public string Apellido { get; set; }
        public string Departamento { get; set; }
        public string Categoria { get; set; }
        public string Sucursal { get; set; }
        public string Horario { get; set; }
        public bool Activo { get; set; } = true;
        public string NombreCompleto => $"{Apellido}, {Nombre} (Legajo: {Legajo})";
    }
}
