using DevsFingerPrint.Domain.DTO;
using DevsFingerPrint.Domain.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;

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
        private string _authToken;

        public ApiClient(string baseUrl)
        {
            _baseUrl = baseUrl.TrimEnd('/');

            // Habilitar TLS 1.2 para peticiones HTTPS seguras
            ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072;
        }

        public bool IniciarSesion(string email, string password)
        {
            try
            {
                // 1. DTO con las claves exactas que espera el backend ("email" y "password")
                var loginDto = new
                {
                    email = email,
                    password = password
                };

                string jsonBody = JsonConvert.SerializeObject(loginDto);
                string url = $"{_baseUrl}/api/auth/login";

                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
                request.Method = "POST";
                request.ContentType = "application/json";

                byte[] byteArray = Encoding.UTF8.GetBytes(jsonBody);
                request.ContentLength = byteArray.Length;

                using (Stream dataStream = request.GetRequestStream())
                {
                    dataStream.Write(byteArray, 0, byteArray.Length);
                }

                using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                using (StreamReader reader = new StreamReader(response.GetResponseStream()))
                {
                    string jsonResponse = reader.ReadToEnd();

                    // 2. Deserializar la respuesta para extraer el JWT
                    var result = JsonConvert.DeserializeObject<LoginResponseDTO>(jsonResponse);

                    if (result != null && !string.IsNullOrEmpty(result.Token))
                    {
                        _authToken = result.Token;
                        return true;
                    }
                }
            }
            catch (WebException ex)
            {
                if (ex.Response is HttpWebResponse errorResponse)
                {
                    using (var reader = new StreamReader(errorResponse.GetResponseStream()))
                    {
                        string errorBody = reader.ReadToEnd();
                        System.Diagnostics.Debug.WriteLine($"[Login Error {(int)errorResponse.StatusCode}]: {errorBody}");
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[Login Error]: {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error general en Login: {ex.Message}");
            }

            return false;
        }

        public List<Huella> ObtenerHuellas(int empresaId)
        {
            if (string.IsNullOrEmpty(_authToken))
            {
                if (!IntentarRenovarSesion()) return null; // Devolver NULL en error para proteger DB local
            }

            try
            {
                // Ajustá esta URL a la ruta exacta de tu HuellasController (ej: /api/huellas/empresa/{empresaId})
                string jsonResponse = RealizarPeticion("GET", $"{_baseUrl}/api/huellas/empresa/{empresaId}", null, _authToken);

                if (!string.IsNullOrEmpty(jsonResponse))
                {
                    return JsonConvert.DeserializeObject<List<Huella>>(jsonResponse) ?? new List<Huella>();
                }
            }
            catch (WebException ex)
            {
                if (ex.Response is HttpWebResponse errorResponse && errorResponse.StatusCode == HttpStatusCode.Unauthorized)
                {
                    if (IntentarRenovarSesion()) return ObtenerHuellas(empresaId);
                }
                System.Diagnostics.Debug.WriteLine($"Error al obtener huellas: {ex.Message}");
            }

            return null; // Retornar NULL ante fallas para no vaciar la DB local
        }

        public List<Empleado> ObtenerEmpleados()
        {
            // 1. Validar que exista una sesión activa
            if (string.IsNullOrEmpty(_authToken))
            {
                System.Diagnostics.Debug.WriteLine("[API Error] No hay token de sesión. Inicie sesión primero.");
                return new List<Empleado>();
            }

            try
            {
                // 2. Hacer la petición GET a /api/empleados incluyendo el Token JWT
                string jsonResponse = RealizarPeticion("GET", $"{_baseUrl}/api/empleados", null, _authToken);

                if (!string.IsNullOrEmpty(jsonResponse))
                {
                    // Deserializar la lista con Newtonsoft.Json
                    List<Empleado> empleados = JsonConvert.DeserializeObject<List<Empleado>>(jsonResponse);
                    return empleados ?? new List<Empleado>();
                }
            }
            catch (WebException ex)
            {
                // 3. Manejar vencimiento del token (401 Unauthorized) con renovación automática
                if (ex.Response is HttpWebResponse errorResponse && errorResponse.StatusCode == HttpStatusCode.Unauthorized)
                {
                    System.Diagnostics.Debug.WriteLine("[API] Token expirado al obtener empleados. Renovando sesión...");

                    string[] creds = CredentialStorage.CargarCredenciales();
                    if (creds != null && creds.Length == 2 && IniciarSesion(creds[0], creds[1]))
                    {
                        // Reintentar la consulta con el token actualizado
                        return ObtenerEmpleados();
                    }
                }

                System.Diagnostics.Debug.WriteLine($"Error al obtener empleados: {ex.Message}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error general al obtener empleados: {ex.Message}");
            }

            return new List<Empleado>();
        }

        public List<Empleado> ObtenerEmpleadosPorEmpresa(int empresaId)
        {
            if (string.IsNullOrEmpty(_authToken)) return new List<Empleado>();

            try
            {
                string jsonResponse = RealizarPeticion("GET", $"{_baseUrl}/api/empleados/empresa/{empresaId}", null, _authToken);

                if (!string.IsNullOrEmpty(jsonResponse))
                {
                    return JsonConvert.DeserializeObject<List<Empleado>>(jsonResponse) ?? new List<Empleado>();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error al obtener empleados por empresa: {ex.Message}");
            }

            return new List<Empleado>();
        }

        public bool EnviarFichadas(List<Fichada> fichadas)
        {
            if (fichadas == null || fichadas.Count == 0) return true;

            if (string.IsNullOrEmpty(_authToken))
            {
                if (!IntentarRenovarSesion()) return false;
            }

            try
            {
                // Mapear de Fichada (Local DB) a FichadaRequestDto (API)
                var listaDtos = fichadas.Select(f => new
                {
                    empleadoId = f.EmpleadoId,
                    fechaHora = f.FechaHora.ToString("yyyy-MM-ddTHH:mm:ss"),
                    tipoRegistro = f.TipoRegistro, // Asegurar que sea "Entrada" o "Salida"
                    metodo = f.Metodo == "BiometricoFAKE" ? "Biometrico" : f.Metodo // Reemplazar valores de prueba no válidos
                }).ToList();

                string jsonBody = JsonConvert.SerializeObject(listaDtos);

                // Llamada al endpoint POST /api/fichadas/bulk (o /api/fichadas)
                string jsonResponse = RealizarPeticion("POST", $"{_baseUrl}/api/fichadas/bulk", jsonBody, _authToken);

                return true;
            }
            catch (WebException ex)
            {
                if (ex.Response is HttpWebResponse errorResponse)
                {
                    using (var stream = errorResponse.GetResponseStream())
                    using (var reader = new System.IO.StreamReader(stream))
                    {
                        System.Diagnostics.Debug.WriteLine($"[API Error {(int)errorResponse.StatusCode}] {reader.ReadToEnd()}");
                    }

                    if (errorResponse.StatusCode == HttpStatusCode.Unauthorized && IntentarRenovarSesion())
                    {
                        return EnviarFichadas(fichadas);
                    }
                }
            }

            return false;
        }

        private bool IntentarRenovarSesion()
        {
            string[] creds = CredentialStorage.CargarCredenciales();
            if (creds != null && creds.Length == 2)
            {
                return IniciarSesion(creds[0], creds[1]);
            }
            System.Diagnostics.Debug.WriteLine("[API Error] No se encontraron credenciales guardadas para auto-login.");
            return false;
        }

        private string RealizarPeticion(string metodo, string url, string jsonBody = null, string token = null)
        {
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
            request.Method = metodo;
            request.ContentType = "application/json";

            // 🔑 Inyectar el Token JWT si fue proporcionado
            if (!string.IsNullOrEmpty(token))
            {
                request.Headers.Add("Authorization", "Bearer " + token);
            }

            if (!string.IsNullOrEmpty(jsonBody) && (metodo == "POST" || metodo == "PUT"))
            {
                byte[] byteArray = Encoding.UTF8.GetBytes(jsonBody);
                request.ContentLength = byteArray.Length;

                using (Stream dataStream = request.GetRequestStream())
                {
                    dataStream.Write(byteArray, 0, byteArray.Length);
                }
            }

            using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
            using (StreamReader reader = new StreamReader(response.GetResponseStream()))
            {
                return reader.ReadToEnd();
            }
        }

        public bool GuardarHuella(Huella huella, int indiceDedo)
        {
            if (string.IsNullOrEmpty(_authToken))
            {
                System.Diagnostics.Debug.WriteLine("Error: Debe iniciar sesión antes de enviar la huella.");
                return false;
            }

            try
            {
                string json = string.Format(
                    "{{\"EmpleadoId\":{0},\"TemplateHuellaBase64\":\"{1}\",\"IndiceDedo\":{2}}}",
                    huella.EmpleadoId,
                    huella.TemplateBiometrico,
                    indiceDedo
                );

                string url = $"{_baseUrl}/api/empleados/enrolar";

                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
                request.Method = "POST";
                request.ContentType = "application/json";

                // 🔑 INCLUIR EL TOKEN JWT EN EL HEADER DE AUTORIZACIÓN
                request.Headers.Add("Authorization", "Bearer " + _authToken);

                byte[] byteArray = Encoding.UTF8.GetBytes(json);
                request.ContentLength = byteArray.Length;

                using (Stream dataStream = request.GetRequestStream())
                {
                    dataStream.Write(byteArray, 0, byteArray.Length);
                }

                using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                {
                    return response.StatusCode == HttpStatusCode.OK ||
                           response.StatusCode == HttpStatusCode.Created;
                }
            }
            catch (WebException ex)
            {
                if (ex.Response is HttpWebResponse errorResponse)
                {
                    using (var reader = new StreamReader(errorResponse.GetResponseStream()))
                    {
                        string errorBody = reader.ReadToEnd();
                        System.Diagnostics.Debug.WriteLine($"[API Error {(int)errorResponse.StatusCode}]: {errorBody}");
                    }
                }
                return false;
            }
        }

        // Helper simple para extraer "token":"valor" sin Newtonsoft si hiciera falta
        private string ExtraerTokenDeJson(string json)
        {
            int tokenIndex = json.IndexOf("\"token\":", StringComparison.OrdinalIgnoreCase);
            if (tokenIndex == -1) return null;

            int start = json.IndexOf('"', tokenIndex + 8) + 1;
            int end = json.IndexOf('"', start);

            if (start > 0 && end > start)
            {
                return json.Substring(start, end - start);
            }
            return null;
        }

        public List<SucursalDTO> ObtenerSucursales()
        {
            if (string.IsNullOrEmpty(_authToken))
            {
                if (!IntentarRenovarSesion()) return new List<SucursalDTO>();
            }

            try
            {
                string jsonResponse = RealizarPeticion("GET", $"{_baseUrl}/api/sucursales", null, _authToken);

                if (!string.IsNullOrEmpty(jsonResponse))
                {
                    // Opción A: Si el endpoint del backend devuelve la entidad con relaciones circulares,
                    // podemos deserializar a un objeto anónimo o lista dinámica primero para evitar el choque de EF.
                    try
                    {
                        return JsonConvert.DeserializeObject<List<SucursalDTO>>(jsonResponse) ?? new List<SucursalDTO>();
                    }
                    catch
                    {
                        // Opción B (Fallback de seguridad): Mapeo manual si el JSON trae propiedades de navegación extra
                        var jsonToken = Newtonsoft.Json.Linq.JToken.Parse(jsonResponse);
                        var listaSucursales = new List<SucursalDTO>();

                        foreach (var item in jsonToken)
                        {
                            listaSucursales.Add(new SucursalDTO
                            {
                                Id = item["Id"]?.Value<int>() ?? item["id"]?.Value<int>() ?? 0,
                                Nombre = item["Nombre"]?.Value<string>() ?? item["nombre"]?.Value<string>(),
                                EmpresaId = item["EmpresaId"]?.Value<int>() ?? item["empresaId"]?.Value<int>() ?? 0,
                                SerialLector = item["SerialLector"]?.Value<string>() ?? item["serialLector"]?.Value<string>()
                            });
                        }
                        return listaSucursales;
                    }
                }
            }
            catch (WebException ex)
            {
                if (ex.Response is HttpWebResponse errorResponse && errorResponse.StatusCode == HttpStatusCode.Unauthorized)
                {
                    if (IntentarRenovarSesion()) return ObtenerSucursales();
                }
                System.Diagnostics.Debug.WriteLine($"Error al obtener sucursales: {ex.Message}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error general al obtener sucursales: {ex.Message}");
            }

            return new List<SucursalDTO>();
        }
    }
}