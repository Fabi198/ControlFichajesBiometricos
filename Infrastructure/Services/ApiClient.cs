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
using System.Threading;

namespace DevsFingerPrint.Infrastructure.Services
{
    public class ApiClient
    {
        private const int TiempoLimiteMs = 15000;
        private readonly string _baseUrl;
        private string _authToken;
        private readonly Func<bool> _renovarSesion;
        private readonly SemaphoreSlim _puertaRenovacion = new SemaphoreSlim(1, 1);
        private int _generacionToken;
        private bool _credencialesRechazadas;

        public string MensajeErrorAutenticacion { get; private set; }
        public bool CredencialesRechazadas { get { return _credencialesRechazadas; } }

        public ApiClient(string baseUrl)
            : this(baseUrl, null)
        {
        }

        public ApiClient(string baseUrl, Func<bool> renovarSesion)
        {
            string normalizada = string.IsNullOrWhiteSpace(baseUrl)
                ? "https://controldeaccesos.online"
                : baseUrl.Trim().TrimEnd('/');
            _baseUrl = normalizada;
            _renovarSesion = renovarSesion;
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
        }

        public bool IniciarSesionAgente(string clientId, string clientSecret)
        {
            MensajeErrorAutenticacion = null;
            AgentLog.Info("Comienzo de login.");
            try
            {
                string url = _baseUrl + "/api/auth/agente";
                string jsonBody = JsonConvert.SerializeObject(new { clientId, clientSecret });
                string responseString = RealizarPeticionSinAuth("POST", url, jsonBody);

                if (string.IsNullOrWhiteSpace(responseString))
                {
                    MensajeErrorAutenticacion = "El servidor respondió con un formato no compatible.";
                    AgentLog.Info("Login rechazado: respuesta vacia.");
                    return false;
                }

                _authToken = ExtraerTokenDeJson(responseString);
                if (string.IsNullOrEmpty(_authToken))
                {
                    MensajeErrorAutenticacion = "El servidor respondió con un formato no compatible.";
                    AgentLog.Info("Login rechazado: respuesta sin token.");
                    return false;
                }

                _credencialesRechazadas = false;
                _generacionToken++;
                AgentLog.Info("Login aceptado.");
                return true;
            }
            catch (ApiHttpException ex)
            {
                MensajeErrorAutenticacion = MensajePorEstado(ex.StatusCode);
                int codigo = (int)ex.StatusCode;
                if (codigo == 400 || codigo == 401 || codigo == 403)
                {
                    _authToken = null;
                    _credencialesRechazadas = true;
                }

                AgentLog.Info("Login rechazado. HTTP " + codigo + ".");
                return false;
            }
            catch (WebException ex)
            {
                MensajeErrorAutenticacion = MensajePorRed(ex);
                AgentLog.Info("Login rechazado. " + ex.Status + ".");
                return false;
            }
            catch (JsonException)
            {
                MensajeErrorAutenticacion = "El servidor respondió con un formato no compatible.";
                AgentLog.Info("Login rechazado: JSON incompatible.");
                return false;
            }
            catch (Exception ex)
            {
                MensajeErrorAutenticacion = "No se pudo conectar con el servidor. Verificá la conexión a Internet.";
                AgentLog.Error("Login con error de conexion: " + ex.GetType().Name + ".");
                return false;
            }
        }

        public bool EnviarHeartbeat()
        {
            if (!HaySesion())
            {
                AgentLog.Info("Heartbeat no enviado: sesion no disponible.");
                return false;
            }

            int generacion = _generacionToken;
            for (int intento = 0; intento < 2; intento++)
            {
                ResultadoHeartbeat resultado = EnviarHeartbeatUnaVez();
                if (resultado == ResultadoHeartbeat.Aceptado)
                {
                    return true;
                }

                if (resultado != ResultadoHeartbeat.NoAutorizado || intento == 1 || !RenovarTras401(generacion))
                {
                    return false;
                }
            }

            return false;
        }

        private ResultadoHeartbeat EnviarHeartbeatUnaVez()
        {
            AgentLog.Info("Inicio de heartbeat.");
            int agenteId = ExtraerAgenteIdDeToken(_authToken);
            if (agenteId <= 0)
            {
                return ResultadoHeartbeat.Fallo;
            }

            try
            {
                string url = _baseUrl + "/api/agentes/" + agenteId + "/heartbeat";
                string response = RealizarPeticion("POST", url, "{}", _authToken);
                if (!string.IsNullOrEmpty(response))
                {
                    AgentLog.Info("Heartbeat aceptado.");
                    return ResultadoHeartbeat.Aceptado;
                }

                AgentLog.Info("Heartbeat sin confirmacion.");
                return ResultadoHeartbeat.Fallo;
            }
            catch (WebException ex)
            {
                HttpStatusCode? statusCode = ObtenerCodigoEstado(ex);
                if (statusCode == HttpStatusCode.Unauthorized)
                {
                    AgentLog.Info("Heartbeat rechazado. HTTP 401.");
                    return ResultadoHeartbeat.NoAutorizado;
                }

                if (statusCode.HasValue)
                {
                    AgentLog.Info("Heartbeat rechazado. HTTP " + (int)statusCode.Value + ".");
                }
                else if (ex.Status == WebExceptionStatus.Timeout || ex.Status == WebExceptionStatus.RequestCanceled)
                {
                    AgentLog.Info("Heartbeat rechazado por timeout.");
                }
                else
                {
                    AgentLog.Info("Heartbeat sin conexion.");
                }

                return ResultadoHeartbeat.Fallo;
            }
            catch (Exception ex)
            {
                AgentLog.Info("Heartbeat rechazado. " + ex.GetType().Name + ".");
                return ResultadoHeartbeat.Fallo;
            }
        }

        public ResultadoLista<Huella> ObtenerHuellas()
        {
            if (!HaySesion())
            {
                return ResultadoLista<Huella>.Fallo("sin-autorizacion");
            }

            int generacion = _generacionToken;
            for (int intento = 0; intento < 2; intento++)
            {
                try
                {
                    int empresaId = ExtraerEmpresaIdDeToken(_authToken);
                    string url = _baseUrl + "/api/huellas/empresa/" + empresaId;
                    string jsonResponse = RealizarPeticion("GET", url, null, _authToken);
                    return InterpretarLista<Huella>(jsonResponse);
                }
                catch (ApiHttpException ex)
                {
                    if (ex.StatusCode == HttpStatusCode.Unauthorized && intento == 0 && RenovarTras401(generacion))
                    {
                        continue;
                    }

                    return ResultadoLista<Huella>.Fallo(MotivoHttp(ex.StatusCode));
                }
                catch (WebException ex)
                {
                    return ResultadoLista<Huella>.Fallo(MotivoTransporte(ex));
                }
                catch (JsonException)
                {
                    return ResultadoLista<Huella>.Fallo("json");
                }
            }

            return ResultadoLista<Huella>.Fallo("sin-autorizacion");
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

        private int ExtraerAgenteIdDeToken(string token)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("[LOG JWT AGENTE] Iniciando extracción de agente_id del token...");

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
                    JObject payload = JObject.Parse(payloadJson);

                    JToken tokenAgente = payload["agente_id"] ?? payload["AgenteId"] ?? payload["agenteId"];
                    if (tokenAgente != null)
                    {
                        int idExtraido = tokenAgente.Value<int>();
                        System.Diagnostics.Debug.WriteLine($"[LOG JWT AGENTE] ÉXITO: Se encontró agente_id en el payload: {idExtraido}");
                        return idExtraido;
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("[LOG JWT AGENTE] ADVERTENCIA: Ninguna de las keys de agente ('agente_id', 'AgenteId', 'agenteId') existe en el payload del token.");
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("[LOG JWT AGENTE] ADVERTENCIA: El formato del token JWT no tiene las partes esperadas.");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[LOG JWT AGENTE ERROR] Error al decodificar agente_id del JWT: " + ex.Message);
            }
            return 0;
        }

        public ResultadoLista<Empleado> ObtenerEmpleados()
        {
            if (!HaySesion())
            {
                return ResultadoLista<Empleado>.Fallo("sin-autorizacion");
            }

            int generacion = _generacionToken;
            for (int intento = 0; intento < 2; intento++)
            {
                try
                {
                    string jsonResponse = RealizarPeticion("GET", _baseUrl + "/api/empleados", null, _authToken);
                    return InterpretarLista<Empleado>(jsonResponse);
                }
                catch (ApiHttpException ex)
                {
                    if (ex.StatusCode == HttpStatusCode.Unauthorized && intento == 0 && RenovarTras401(generacion))
                    {
                        continue;
                    }

                    return ResultadoLista<Empleado>.Fallo(MotivoHttp(ex.StatusCode));
                }
                catch (WebException ex)
                {
                    return ResultadoLista<Empleado>.Fallo(MotivoTransporte(ex));
                }
                catch (JsonException)
                {
                    return ResultadoLista<Empleado>.Fallo("json");
                }
            }

            return ResultadoLista<Empleado>.Fallo("sin-autorizacion");
        }

        internal static string MotivoTransporte(WebException exception)
        {
            if (exception == null)
            {
                return "red";
            }

            if (exception.Status == WebExceptionStatus.Timeout || exception.Status == WebExceptionStatus.RequestCanceled)
            {
                return "timeout";
            }

            return "red";
        }

        private static string MotivoHttp(HttpStatusCode statusCode)
        {
            return "http-" + (int)statusCode;
        }

        private static ResultadoLista<T> InterpretarLista<T>(string jsonResponse)
        {
            if (string.IsNullOrWhiteSpace(jsonResponse))
            {
                return ResultadoLista<T>.Fallo("json");
            }

            List<T> lista = JsonConvert.DeserializeObject<List<T>>(jsonResponse);
            if (lista == null)
            {
                return ResultadoLista<T>.Fallo("json");
            }

            return ResultadoLista<T>.Ok(lista);
        }

        public bool EnviarFichadas(List<Fichada> fichadas)
        {
            if (fichadas == null || fichadas.Count == 0 || fichadas.Count > 500) return false;
            if (!HaySesion()) return false;
            int generacion = _generacionToken;
            for (int intento = 0; intento < 2; intento++)
            {
                try
                {
                    var solicitudes = fichadas.Select(f => new
                    {
                        empleadoId = f.EmpleadoId,
                        fechaHora = f.FechaHora
                    }).ToList();
                    string jsonBody = JsonConvert.SerializeObject(solicitudes);
                    string response = RealizarPeticion("POST", _baseUrl + "/api/fichadas/biometricas/bulk", jsonBody, _authToken);
                    int? recibidas = JObject.Parse(response)["recibidas"]?.Value<int>();
                    return recibidas == solicitudes.Count;
                }
                catch (WebException ex)
                {
                    if (ObtenerCodigoEstado(ex) == HttpStatusCode.Unauthorized && intento == 0 && RenovarTras401(generacion))
                    {
                        continue;
                    }
                }
                catch (JsonException)
                {
                }

                return false;
            }

            return false;
        }

        public bool GuardarHuella(Huella huella, int indiceDedo)
        {
            if (!HaySesion()) return false;
            int generacion = _generacionToken;
            for (int intento = 0; intento < 2; intento++)
            {
                try
                {
                    var payload = new
                    {
                        empleadoId = huella.EmpleadoId,
                        indiceDedo = indiceDedo,
                        templateHuellaBase64 = huella.TemplateBiometrico
                    };

                    string jsonBody = JsonConvert.SerializeObject(payload);
                    string response = RealizarPeticion("POST", _baseUrl + "/api/empleados/enrolar", jsonBody, _authToken);
                    return !string.IsNullOrEmpty(response);
                }
                catch (WebException ex)
                {
                    if (ObtenerCodigoEstado(ex) == HttpStatusCode.Unauthorized && intento == 0 && RenovarTras401(generacion))
                    {
                        continue;
                    }
                }
                catch (Exception)
                {
                }

                return false;
            }

            return false;
        }

        private bool HaySesion()
        {
            return !_credencialesRechazadas && !string.IsNullOrEmpty(_authToken);
        }

        private bool RenovarTras401(int generacionVista)
        {
            _puertaRenovacion.Wait();
            try
            {
                if (_generacionToken != generacionVista)
                {
                    return HaySesion();
                }

                if (_credencialesRechazadas)
                {
                    return false;
                }

                bool ok = false;
                try
                {
                    if (_renovarSesion != null)
                    {
                        ok = _renovarSesion();
                    }
                    else if (CredentialStorage.CargarCredenciales(out string clientId, out string clientSecret))
                    {
                        ok = IniciarSesionAgente(clientId, clientSecret);
                    }
                }
                catch (Exception ex)
                {
                    AgentLog.Error("Renovacion con error: " + ex.GetType().Name + ".");
                    ok = false;
                }

                return ok && HaySesion();
            }
            finally
            {
                _puertaRenovacion.Release();
            }
        }

        private enum ResultadoHeartbeat
        {
            Aceptado,
            NoAutorizado,
            Fallo
        }

        private string RealizarPeticion(string metodo, string url, string jsonBody = null, string token = null)
        {
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
            ConfigurarSolicitud(request);
            request.Method = metodo;
            request.ContentType = "application/json";

            if (!string.IsNullOrEmpty(token))
            {
                // Forma correcta para HttpWebRequest en .NET Framework
                request.Headers[HttpRequestHeader.Authorization] = "Bearer " + token;
            }

            EscribirCuerpo(request, metodo, jsonBody);
            return LeerRespuesta(request);
        }

        private string RealizarPeticionSinAuth(string metodo, string url, string jsonBody)
        {
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
            ConfigurarSolicitud(request);
            request.Method = metodo;
            request.ContentType = "application/json";
            EscribirCuerpo(request, metodo, jsonBody);
            return LeerRespuesta(request);
        }

        private static void ConfigurarSolicitud(HttpWebRequest request)
        {
            request.Timeout = TiempoLimiteMs;
            request.ReadWriteTimeout = TiempoLimiteMs;
        }

        private static void EscribirCuerpo(HttpWebRequest request, string metodo, string jsonBody)
        {
            if (string.IsNullOrEmpty(jsonBody) || (metodo != "POST" && metodo != "PUT"))
            {
                return;
            }

            byte[] data = Encoding.UTF8.GetBytes(jsonBody);
            request.ContentLength = data.Length;
            using (Stream stream = request.GetRequestStream())
            {
                stream.Write(data, 0, data.Length);
            }
        }

        private static string LeerRespuesta(HttpWebRequest request)
        {
            try
            {
                using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                using (StreamReader reader = new StreamReader(response.GetResponseStream()))
                {
                    return reader.ReadToEnd();
                }
            }
            catch (WebException ex)
            {
                if (ex.Status == WebExceptionStatus.Timeout || ex.Status == WebExceptionStatus.RequestCanceled)
                {
                    request.Abort();
                }

                if (ex.Response is HttpWebResponse errorResponse)
                {
                    int statusCode = (int)errorResponse.StatusCode;
                    string statusDescription = errorResponse.StatusDescription;
                    errorResponse.Close();
                    System.Diagnostics.Debug.WriteLine($"[ERROR API HTTP {statusCode} {statusDescription}]");
                    throw new ApiHttpException((HttpStatusCode)statusCode, statusDescription, ex);
                }

                request.Abort();
                throw;
            }
        }

        private static string MensajePorEstado(HttpStatusCode statusCode)
        {
            int code = (int)statusCode;
            if (code == 400 || code == 401)
            {
                return "Las credenciales del agente no son válidas.";
            }

            if (code == 403)
            {
                return "El agente no tiene autorización para conectarse.";
            }

            if (code >= 500 && code <= 599)
            {
                return "El servidor no está disponible temporalmente.";
            }

            return "El servidor respondió con un formato no compatible.";
        }

        private static string MensajePorRed(WebException exception)
        {
            if (exception.Status == WebExceptionStatus.Timeout || exception.Status == WebExceptionStatus.RequestCanceled)
            {
                return "El servidor tardó demasiado en responder. Intentá nuevamente.";
            }

            if (exception.Response is HttpWebResponse response)
            {
                return MensajePorEstado(response.StatusCode);
            }

            return "No se pudo conectar con el servidor. Verificá la conexión a Internet.";
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
            AgentLog.Info("Inicio de sincronizacion de sucursal.");
            System.Diagnostics.Debug.WriteLine("[LOG SYNC] Iniciando SincronizarSucursal() para Agente...");

            if (!HaySesion())
            {
                return false;
            }

            int generacion = _generacionToken;
            for (int intento = 0; intento < 2; intento++)
            {
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
                System.Diagnostics.Debug.WriteLine("[LOG SYNC] Respuesta de sucursal recibida.");

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

                        AgentLog.Info("Sincronizacion de sucursal completada.");
                        System.Diagnostics.Debug.WriteLine("[LOG API] Sucursal sincronizada y guardada localmente mediante Agente.");
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
                if (ObtenerCodigoEstado(ex) == HttpStatusCode.Unauthorized && intento == 0 && RenovarTras401(generacion))
                {
                    continue;
                }
            }
            catch (Exception)
            {
            }

            return false;
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

        private static HttpStatusCode? ObtenerCodigoEstado(WebException exception)
        {
            if (exception is ApiHttpException apiException)
            {
                return apiException.StatusCode;
            }

            return (exception.Response as HttpWebResponse)?.StatusCode;
        }

        private sealed class ApiHttpException : WebException
        {
            public HttpStatusCode StatusCode { get; }

            public ApiHttpException(HttpStatusCode statusCode, string description, WebException innerException)
                : base($"HTTP {(int)statusCode}: {description}", innerException, WebExceptionStatus.ProtocolError, null)
            {
                StatusCode = statusCode;
            }
        }
    }
}