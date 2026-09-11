using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DevsFingerPrint.Domain.DTO
{
    public class SucursalDTO
    {
        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("nombre")]
        public string Nombre { get; set; }

        [JsonProperty("empresaId")]
        public int EmpresaId { get; set; }

        [JsonProperty("serialLector")]
        public string SerialLector { get; set; }
    }
}
