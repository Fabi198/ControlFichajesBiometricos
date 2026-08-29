using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DevsFingerPrint.Domain.Models
{
    public class LoginResponseDTO
    {

        public string Token { get; set; } = string.Empty;
        public string Mensaje { get; set; } = string.Empty;


    }
}
