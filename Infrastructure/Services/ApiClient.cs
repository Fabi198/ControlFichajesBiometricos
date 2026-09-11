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

        public bool SincronizarSucursal()
        {
            System.Diagnostics.Debug.WriteLine("[LOG SYNC] Iniciando SincronizarSucursal() para Agente...");

            if (string.IsNullOrEmpty(_authToken) && !IntentarRenovarSesion())
            {
                System.Diagnostics.Debug.WriteLine("[LOG SYNC] FALLO: No hay _authToken y falló la renovación de sesión.");
                return false;
            }

            try
            {
                // Opcional: Se mantiene la extracción solo para fines de log y diagnóstico,
                // ya que el nuevo endpoint resuelve la sucursal a partir del token del agente.
                int sucursalId = ExtraerSucursalIdDeToken(_authToken);
                if (sucursalId > 0)
                {
                    System.Diagnostics.Debug.WriteLine($"[LOG SYNC] SucursalId detectado en token de agente: {sucursalId}");
                }

                // Nuevo endpoint específico para agentes autenticados
                string url = $"{_baseUrl}/api/agentes/sucursal";
                System.Diagnostics.Debug.WriteLine($"[LOG SYNC] Realizando petición GET a: {url}");

                string jsonResponse = RealizarPeticion("GET", url, null, _authToken);
                System.Diagnostics.Debug.WriteLine($"[LOG SYNC] Respuesta recibida de la API: {(string.IsNullOrEmpty(jsonResponse) ? "VACÍA/NULL" : jsonResponse)}");

                if (!string.IsNullOrEmpty(jsonResponse))
                {
                    SucursalDTO sucursal = JsonConvert.DeserializeObject<SucursalDTO>(jsonResponse);
                    if (sucursal != null)
                    {
                        System.Diagnostics.Debug.WriteLine($"[LOG SYNC] Deserialización exitosa -> ID: {sucursal.Id}, Nombre: {sucursal.Nombre}, EmpresaId: {sucursal.EmpresaId}, SerialLector: '{sucursal.SerialLector}'");

                        DevsFingerPrint.Infrastructure.Data.LocalDatabase.GuardarSucursalLocal(
                            sucursal.Id,
                            sucursal.Nombre,
                            sucursal.EmpresaId,
                            sucursal.SerialLector
                        );

                        System.Diagnostics.Debug.WriteLine($"[LOG API] Sucursal '{sucursal.Nombre}' sincronizada y guardada localmente mediante Agente.");
                        return true;
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("[LOG SYNC] FALLO: SucursalDTO quedó en null después de deserializar.");
                    }
                }
            }
            catch (WebException ex)
            {
                if (ex.Response is HttpWebResponse err && err.StatusCode == HttpStatusCode.Unauthorized && IntentarRenovarSesion())
                {
                    System.Diagnostics.Debug.WriteLine("[LOG SYNC] Token expirado (401), reintentando SincronizarSucursal()...");
                    return SincronizarSucursal();
                }
                System.Diagnostics.Debug.WriteLine($"Error al sincronizar sucursal (WebException): {ex.Message}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error general al sincronizar sucursal: {ex.Message}");
            }
            return false;
        }

        private int ExtraerSucursalIdDeToken(string token)
        {
            try
            {
                string[] parts = token.Split('.');
                if (parts.Length > 1)
                {
                    string base64 = parts[1].Replace('-', '+').Replace('_', '/');
                    switch (base64.Length % 4)
                    {
                        case 2: base64 += "=="; break;
                        case 3: base64 += "="; break;
                    }

                    byte[] data = Convert.FromBase64String(base64);
                    string payloadJson = Encoding.UTF8.GetString(data);
                    System.Diagnostics.Debug.WriteLine($"[LOG JWT Payload]: {payloadJson}");

                    JObject payload = JObject.Parse(payloadJson);

                    JToken tokenSucursal = payload["sucursal_id"] ?? payload["SucursalId"] ?? payload["sucursalId"];
                    if (tokenSucursal != null)
                    {
                        int idExtraido = tokenSucursal.Value<int>();
                        System.Diagnostics.Debug.WriteLine($"[LOG JWT] Se encontró sucursal_id en el payload: {idExtraido}");
                        return idExtraido;
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("[LOG JWT] ADVERTENCIA: Ninguna de las keys de sucursal ('sucursal_id', 'SucursalId', 'sucursalId') existe en el payload del token.");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Error al decodificar sucursal_id del JWT: " + ex.Message);
            }
            return 0;
        }

        public bool ValidarSerialLector(string serialLectorFisico)
        {
            System.Diagnostics.Debug.WriteLine("[LOG VALIDAR] Iniciando validación de serial del lector físico...");

            serialLectorFisico = serialLectorFisico.Trim('{', '}', ' ', '\r', '\n'); ;

            












            serialLectorFisico = LimpiarSerial(serialLectorFisico);


            if (string.IsNullOrEmpty(serialLectorFisico))
            {
                System.Diagnostics.Debug.WriteLine("[LOG VALIDAR] FALLO: El serial del lector físico proporcionado está vacío o nulo.");
                return false;
            }

            // 1. Obtener el serial guardado en la base de datos local
            string serialLocal = LimpiarSerial(Data.LocalDatabase.ObtenerSerialLectorLocal());
            System.Diagnostics.Debug.WriteLine($"[LOG VALIDAR] Serial local recuperado: '{serialLocal ?? "N/A"}'");

            bool coincideLocal = string.Equals(serialLectorFisico, serialLocal, StringComparison.OrdinalIgnoreCase);
            bool verificacionOnlineExitosa = false;

            // 2. Intentar cotejar contra la API (Remota) con manejo de errores por falta de internet
            try
            {
                string url = $"{_baseUrl}/api/agentes/sucursal";
                string jsonResponse = RealizarPeticion("GET", url, null, _authToken);

                if (!string.IsNullOrEmpty(jsonResponse))
                {
                    SucursalDTO sucursalRemota = JsonConvert.DeserializeObject<SucursalDTO>(jsonResponse);
                    if (sucursalRemota != null)
                    {
                        string serialRemoto = LimpiarSerial(sucursalRemota.SerialLector);
                        System.Diagnostics.Debug.WriteLine($"[LOG VALIDAR] Serial remoto (API) recuperado: '{serialRemoto ?? "N/A"}'");


                        if (!string.IsNullOrEmpty(serialRemoto) && !string.Equals(serialRemoto, serialLocal, StringComparison.OrdinalIgnoreCase))
                        {
                            System.Diagnostics.Debug.WriteLine($"[LOG SYNC] El serial de la API cambió ('{serialLocal}' -> '{serialRemoto}'). Actualizando base de datos local...");

                            // Reutilizamos el método existente pasando los datos actualizados de la sucursal remota
                            DevsFingerPrint.Infrastructure.Data.LocalDatabase.GuardarSucursalLocal(
                                sucursalRemota.Id,
                                sucursalRemota.Nombre,
                                sucursalRemota.EmpresaId,
                                sucursalRemota.SerialLector
                            );
                        }


                        bool coincideRemoto = string.Equals(serialLectorFisico, serialRemoto, StringComparison.OrdinalIgnoreCase);

                        if (!coincideRemoto)
                        {
                            System.Diagnostics.Debug.WriteLine("[LOG VALIDAR] FALLO: El serial físico NO coincide con el registrado en el servidor/API.");
                            return false;
                        }

                        verificacionOnlineExitosa = true;
                        System.Diagnostics.Debug.WriteLine("[LOG VALIDAR] ÉXITO: El serial físico coincide con la API.");
                    }
                }
            }
            catch (Exception ex)
            {
                // Sin internet o caída de red: capturamos la excepción para no romper el flujo
                System.Diagnostics.Debug.WriteLine($"[LOG VALIDAR] MODO OFFLINE ACTIVADO: No se pudo conectar a la API ({ex.Message}). Cotejando exclusivamente con la base local...");
            }

            // 3. Si no hubo conexión a la API, recurrimos al respaldo local obligatorio
            if (!verificacionOnlineExitosa)
            {
                if (!coincideLocal)
                {
                    System.Diagnostics.Debug.WriteLine("[LOG VALIDAR] FALLO (Offline): El serial físico tampoco coincide con el registro local.");
                    return false;
                }

                System.Diagnostics.Debug.WriteLine("[LOG VALIDAR] ÉXITO (Offline): Validación superada mediante la base de datos local.");
                return true;
            }

            return true;
        }

        private string LimpiarSerial(string serial)
        {
            if (string.IsNullOrEmpty(serial)) return string.Empty;

            // Remueve caracteres nulos (\0), de control, espacios y normaliza a mayúsculas
            return new string(serial.Where(c => !char.IsControl(c) && c != '\0' && !char.IsWhiteSpace(c)).ToArray()).ToUpper();
        }
    }
}