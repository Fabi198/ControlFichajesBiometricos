using DevsFingerPrint.Domain.DTO;
using DevsFingerPrint.Domain.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;

namespace DevsFingerPrint.Infrastructure.Services
{
    public class ApiClient
    {
        private readonly string _baseUrl;
        private string _authToken;

        public ApiClient(string baseUrl)
        {
            _baseUrl = baseUrl.TrimEnd('/');
            ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072;
        }

        public bool IniciarSesionAgente(string clientId, string clientSecret)
        {
            try
            {
                string url = $"{_baseUrl}/api/auth/agente";
                string jsonBody = "{\"clientId\":\"" + clientId + "\", \"clientSecret\":\"" + clientSecret + "\"}";
                string responseString = RealizarPeticionSinAuth("POST", url, jsonBody);

                if (!string.IsNullOrEmpty(responseString))
                {
                    _authToken = ExtraerTokenDeJson(responseString);
                    System.Diagnostics.Debug.WriteLine("TOKEN ACTUAL: " + _authToken);
                    return !string.IsNullOrEmpty(_authToken);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Error en login de agente: " + ex.Message);
            }
            return false;
        }

        public bool EnviarHeartbeat(int agenteId)
        {
            if (string.IsNullOrEmpty(_authToken) && !IntentarRenovarSesion()) return false;
            try
            {
                string url = $"{_baseUrl}/api/agentes/{agenteId}/heartbeat";
                string response = RealizarPeticion("POST", url, null, _authToken);
                return !string.IsNullOrEmpty(response);
            }
            catch (WebException ex)
            {
                if (ex.Response is HttpWebResponse err && err.StatusCode == HttpStatusCode.Unauthorized && IntentarRenovarSesion())
                {
                    return EnviarHeartbeat(agenteId);
                }
                System.Diagnostics.Debug.WriteLine($"Error en heartbeat: {ex.Message}");
            }
            return false;
        }

        public List<Huella> ObtenerHuellas()
        {
            if (string.IsNullOrEmpty(_authToken) && !IntentarRenovarSesion()) return null;

            try
            {
                // Extraemos el empresa_id del token JWT del agente
                int empresaId = ExtraerEmpresaIdDeToken(_authToken);
                string url = $"{_baseUrl}/api/huellas/empresa/{empresaId}";

                string jsonResponse = RealizarPeticion("GET", url, null, _authToken);
                return string.IsNullOrEmpty(jsonResponse) ? new List<Huella>() : JsonConvert.DeserializeObject<List<Huella>>(jsonResponse);
            }
            catch (WebException ex)
            {
                if (ex.Response is HttpWebResponse err && err.StatusCode == HttpStatusCode.Unauthorized && IntentarRenovarSesion())
                {
                    return ObtenerHuellas();
                }
                System.Diagnostics.Debug.WriteLine($"Error al obtener huellas: {ex.Message}");
            }
            return null;
        }

        private int ExtraerEmpresaIdDeToken(string token)
        {
            try
            {
                string[] parts = token.Split('.');
                if (parts.Length > 1)
                {
                    // Base64Url a Base64 estándar
                    string base64 = parts[1].Replace('-', '+').Replace('_', '/');
                    switch (base64.Length % 4)
                    {
                        case 2: base64 += "=="; break;
                        case 3: base64 += "="; break;
                    }

                    byte[] data = Convert.FromBase64String(base64);
                    string payloadJson = Encoding.UTF8.GetString(data);
                    JObject payload = JObject.Parse(payloadJson);

                    JToken tokenEmpresa = payload["empresa_id"] ?? payload["EmpresaId"];
                    if (tokenEmpresa != null)
                    {
                        return tokenEmpresa.Value<int>();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Error al decodificar JWT: " + ex.Message);
            }
            return 0;
        }

        public List<Empleado> ObtenerEmpleados()
        {
            if (string.IsNullOrEmpty(_authToken) && !IntentarRenovarSesion()) return new List<Empleado>();
            try
            {
                string jsonResponse = RealizarPeticion("GET", $"{_baseUrl}/api/empleados", null, _authToken);
                return string.IsNullOrEmpty(jsonResponse) ? new List<Empleado>() : JsonConvert.DeserializeObject<List<Empleado>>(jsonResponse);
            }
            catch (WebException ex)
            {
                HttpWebResponse err = ex.Response as HttpWebResponse;
                if (err != null && err.StatusCode == HttpStatusCode.Unauthorized)
                {
                    if (IntentarRenovarSesion())
                    {
                        return ObtenerEmpleados();
                    }
                }
                System.Diagnostics.Debug.WriteLine("Error al obtener empleados: " + ex.Message);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Error general al obtener empleados: " + ex.Message);
            }
            return new List<Empleado>();
        }

        public bool EnviarFichadas(List<Fichada> fichadas)
        {
            if (string.IsNullOrEmpty(_authToken) && !IntentarRenovarSesion()) return false;
            try
            {
                string jsonBody = JsonConvert.SerializeObject(fichadas);
                string response = RealizarPeticion("POST", $"{_baseUrl}/api/fichadas/bulk", jsonBody, _authToken);
                return !string.IsNullOrEmpty(response);
            }
            catch (WebException ex)
            {
                if (ex.Response is HttpWebResponse err && err.StatusCode == HttpStatusCode.Unauthorized && IntentarRenovarSesion())
                {
                    return EnviarFichadas(fichadas);
                }
                System.Diagnostics.Debug.WriteLine($"Error al enviar fichadas: {ex.Message}");
            }
            return false;
        }

        public bool GuardarHuella(Huella huella, int indiceDedo)
        {
            if (string.IsNullOrEmpty(_authToken) && !IntentarRenovarSesion()) return false;
            try
            {
                var payload = new
                {
                    empleadoId = huella.EmpleadoId,
                    indiceDedo = indiceDedo,
                    templateHuellaBase64 = huella.TemplateBiometrico
                };

                string jsonBody = JsonConvert.SerializeObject(payload);

                // Ruta corregida según el contrato de la API
                string url = $"{_baseUrl}/api/empleados/enrolar";

                string response = RealizarPeticion("POST", url, jsonBody, _authToken);
                return !string.IsNullOrEmpty(response);
            }
            catch (WebException ex)
            {
                HttpWebResponse err = ex.Response as HttpWebResponse;
                if (err != null && err.StatusCode == HttpStatusCode.Unauthorized)
                {
                    if (IntentarRenovarSesion())
                    {
                        return GuardarHuella(huella, indiceDedo);
                    }
                }
                System.Diagnostics.Debug.WriteLine("Error al guardar huella: " + ex.Message);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Error general al guardar huella: " + ex.Message);
            }
            return false;
        }

        private bool IntentarRenovarSesion()
        {
            try
            {
                if (CredentialStorage.CargarCredenciales(out string clientId, out string clientSecret))
                {
                    return IniciarSesionAgente(clientId, clientSecret);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Excepción al renovar sesión: {ex.Message}");
            }
            return false;
        }

        private string RealizarPeticion(string metodo, string url, string jsonBody = null, string token = null)
        {
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
            request.Method = metodo;
            request.ContentType = "application/json";

            if (!string.IsNullOrEmpty(token))
            {
                request.Headers.Add("Authorization", "Bearer " + token);
            }

            if (!string.IsNullOrEmpty(jsonBody) && (metodo == "POST" || metodo == "PUT"))
            {
                byte[] data = Encoding.UTF8.GetBytes(jsonBody);
                request.ContentLength = data.Length;
                using (Stream stream = request.GetRequestStream())
                {
                    stream.Write(data, 0, data.Length);
                }
            }

            using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
            using (StreamReader reader = new StreamReader(response.GetResponseStream()))
            {
                return reader.ReadToEnd();
            }
        }

        private string RealizarPeticionSinAuth(string metodo, string url, string jsonBody)
        {
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
            request.Method = metodo;
            request.ContentType = "application/json";

            if (!string.IsNullOrEmpty(jsonBody))
            {
                byte[] data = Encoding.UTF8.GetBytes(jsonBody);
                request.ContentLength = data.Length;
                using (Stream stream = request.GetRequestStream())
                {
                    stream.Write(data, 0, data.Length);
                }
            }

            using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
            using (StreamReader reader = new StreamReader(response.GetResponseStream()))
            {
                return reader.ReadToEnd();
            }
        }

        private string ExtraerTokenDeJson(string json)
        {
            int tokenIndex = json.IndexOf("\"token\":", StringComparison.OrdinalIgnoreCase);
            if (tokenIndex == -1) return null;

            int start = json.IndexOf('"', json.IndexOf(':', tokenIndex)) + 1;
            int end = json.IndexOf('"', start);

            return (start > 0 && end > start) ? json.Substring(start, end - start) : null;
        }
    }
}