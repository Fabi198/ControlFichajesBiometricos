using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using DevsFingerPrint.Domain.Models;
using Newtonsoft.Json;

namespace DevsFingerPrint.Infrastructure.Services
{
    
    public class CatalogoResponse
    {
        public List<Empleado> Empleados { get; set; } = new List<Empleado>();
        public List<Huella> Huellas { get; set; } = new List<Huella>();
    }

    public class ApiClient
    {
        private readonly string _baseUrl;

        public ApiClient(string baseUrl)
        {
            _baseUrl = baseUrl.TrimEnd('/');

            // Habilitar TLS 1.2 para peticiones HTTPS seguras
            ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072;
        }

        public CatalogoResponse ObtenerCatalogo(int empresaId)
        {
            var respuesta = new CatalogoResponse();

            try
            {
                string jsonEmp = RealizarPeticion("GET", $"{_baseUrl}/api/empleados/empresa/{empresaId}", null);
                string jsonHue = RealizarPeticion("GET", $"{_baseUrl}/api/huellas/empresa/{empresaId}", null);

                if (!string.IsNullOrEmpty(jsonEmp))
                {
                    respuesta.Empleados = JsonConvert.DeserializeObject<List<Empleado>>(jsonEmp) ?? new List<Empleado>();
                }

                if (!string.IsNullOrEmpty(jsonHue))
                {
                    respuesta.Huellas = JsonConvert.DeserializeObject<List<Huella>>(jsonHue) ?? new List<Huella>();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error al obtener catálogo desde la API: {ex.Message}");
            }

            return respuesta;
        }

        public bool EnviarFichadas(IEnumerable<Fichada> fichadas)
        {
            try
            {
                string jsonBody = JsonConvert.SerializeObject(fichadas);
                string response = RealizarPeticion("POST", $"{_baseUrl}/api/fichadas/bulk", jsonBody);
                return response != null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error al enviar fichadas: {ex.Message}");
                return false;
            }
        }

        private string RealizarPeticion(string metodo, string url, string jsonBody)
        {
            var request = (HttpWebRequest)WebRequest.Create(url);
            request.Method = metodo;
            request.ContentType = "application/json";
            request.Timeout = 10000;

            if (!string.IsNullOrEmpty(jsonBody) && (metodo == "POST" || metodo == "PUT"))
            {
                byte[] data = Encoding.UTF8.GetBytes(jsonBody);
                request.ContentLength = data.Length;

                using (Stream stream = request.GetRequestStream())
                {
                    stream.Write(data, 0, data.Length);
                }
            }

            using (var response = (HttpWebResponse)request.GetResponse())
            {
                if (response.StatusCode == HttpStatusCode.OK || response.StatusCode == HttpStatusCode.Created)
                {
                    using (var reader = new StreamReader(response.GetResponseStream(), Encoding.UTF8))
                    {
                        return reader.ReadToEnd();
                    }
                }
            }
            return null;
        }
    }
}