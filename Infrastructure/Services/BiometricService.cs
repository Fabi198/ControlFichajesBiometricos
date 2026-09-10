using DevsFingerPrint.Domain.Interfaces;
using DevsFingerPrint.Domain.Models;
using DPUruNet;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows.Forms;

namespace DevsFingerPrint.Infrastructure.Services
{
    public class BiometricService : IBiometricService
    {
        private Reader lector;
        private Reader.CaptureCallback onCapturedDelegate; // Referencia persistente para evitar Garbage Collection
        private bool esEnrolamiento;
        private List<Fmd> fmdsEnrolamiento;
        private const int CAPTURAS_REQUERIDAS_ENROLAMIENTO = 4;
        public bool listo = false;

        // Controla si el servicio debe seguir re-armando la captura después de cada evento.
        // Sin esto, CaptureAsync solo dispara UNA vez y el lector queda "muerto".
        private volatile bool lecturaActiva;

        // Evita procesar dos veces la misma huella si el usuario deja el dedo apoyado
        // y el lector se re-arma muy rápido (anti-rebote / anti doble fichada).
        private DateTime ultimaCapturaProcesada = DateTime.MinValue;
        private static readonly TimeSpan COOLDOWN_ENTRE_CAPTURAS = TimeSpan.FromSeconds(3);

        // Eventos de la interfaz
        public event Action<Fmd> OnHuellaCapturada;
        public event Action<int> OnEmpleadoIdentificado;
        public event Action OnHuellaNoReconocida;
        public event Action<string> OnEstadoCambiado;

        event Action<int> IBiometricService.OnProgresoEnrolamiento
        {
            add => OnProgresoEnrolamientoInternal += value;
            remove => OnProgresoEnrolamientoInternal -= value;
        }
        private Action<int> OnProgresoEnrolamientoInternal;

        public BiometricService()
        {
            fmdsEnrolamiento = new List<Fmd>();
            Debug.WriteLine("[LOG BiometricService] Instancia creada.");
        }

        public Reader ObtenerLectorActual()
        {
            if (lector == null)
            {
                ReaderCollection readers = ReaderCollection.GetReaders();
                if (readers != null && readers.Count > 0)
                {
                    lector = readers[0];
                    Debug.WriteLine($"[LOG Biometric] Lector encontrado: {lector.Description.SerialNumber}");
                }
                else
                {
                    Debug.WriteLine("[LOG Biometric ERROR] No se detectó ningún lector al consultar ObtenerLectorActual.");
                }
            }
            return lector;
        }

        public bool IniciarLectura()
        {
            Debug.WriteLine("[LOG Biometric] Intentando iniciar lectura continua del sensor...");
            try
            {
                ReaderCollection listaLectores = ReaderCollection.GetReaders();

                if (listaLectores == null || listaLectores.Count == 0)
                {
                    Debug.WriteLine("[LOG Biometric ERROR] Lista de lectores vacía. Verifique la conexión USB.");
                    OnEstadoCambiado?.Invoke("No se detectó ningún lector biométrico conectado.");
                    MessageBox.Show(
                        "No se detectó ningún lector biométrico conectado.\nLa aplicación no puede funcionar sin el dispositivo y se cerrará.",
                        "Error de Hardware",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error
                    );
                    Environment.Exit(0);
                    return false;
                }

                lector = listaLectores[0];
                Debug.WriteLine($"[LOG Biometric] Lector seleccionado: {lector.Description.SerialNumber} - {lector.Description.Name}");

                // 1. Abrir lector en modo compartido/cooperativo
                Constants.ResultCode resultado = lector.Open(Constants.CapturePriority.DP_PRIORITY_EXCLUSIVE);
                if (resultado != Constants.ResultCode.DP_SUCCESS)
                {
                    Debug.WriteLine($"[LOG Biometric ERROR] Falló lector.Open(). Código de resultado: {resultado}");
                    OnEstadoCambiado?.Invoke($"Error al abrir el lector: {resultado}");
                    return false;
                }

                // Crear y mantener vivo el delegado
                onCapturedDelegate = new Reader.CaptureCallback(OnCapturaRealizada);

                // Desuscribir previo para evitar duplicados y suscribir la referencia viva
                lector.On_Captured -= onCapturedDelegate;
                lector.On_Captured += onCapturedDelegate;

                // A partir de acá el servicio debe mantenerse re-armando la captura
                // cada vez que el evento On_Captured se dispare.
                lecturaActiva = true;

                bool armado = ArmarSiguienteCaptura();
                if (!armado)
                {
                    lecturaActiva = false;
                }

                return armado;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[LOG Biometric EXCEPCIÓN] Error en IniciarLectura: {ex.Message}\n{ex.StackTrace}");
                OnEstadoCambiado?.Invoke($"Excepción al iniciar lector: {ex.Message}");
                lecturaActiva = false;
                return false;
            }
        }

        /// <summary>
        /// Arma una nueva captura asíncrona en el lector. Esto es lo que hay que volver a
        /// llamar cada vez que el evento On_Captured termina de procesarse, porque
        /// CaptureAsync en este SDK dispara UNA sola vez por llamada.
        /// </summary>
        private bool ArmarSiguienteCaptura()
        {
            if (lector == null) return false;

            try
            {
                // Chequear el estado antes de rearmar: si el lector está ocupado o
                // necesita calibración, hay que resolverlo primero (igual que hace
                // la muestra oficial de DigitalPersona antes de cada captura).
                Constants.ResultCode statusResult = lector.GetStatus();
                if (statusResult != Constants.ResultCode.DP_SUCCESS)
                {
                    Debug.WriteLine($"[LOG Biometric ERROR] GetStatus falló al rearmar: {statusResult}");
                    OnEstadoCambiado?.Invoke($"Error de estado del lector: {statusResult}");
                    return false;
                }

                if (lector.Status.Status == Constants.ReaderStatuses.DP_STATUS_BUSY)
                {
                    // Reintenta en breve en vez de perder la captura.
                    System.Threading.ThreadPool.QueueUserWorkItem(_ =>
                    {
                        System.Threading.Thread.Sleep(50);
                        if (lecturaActiva) ArmarSiguienteCaptura();
                    });
                    return true;
                }

                if (lector.Status.Status == Constants.ReaderStatuses.DP_STATUS_NEED_CALIBRATION)
                {
                    lector.Calibrate();
                }

                // Intentar formato ANSI primero
                Constants.ResultCode resultadoStart = lector.CaptureAsync(
                    Constants.Formats.Fid.ANSI,
                    Constants.CaptureProcessing.DP_IMG_PROC_DEFAULT,
                    lector.Capabilities.Resolutions[0]
                );

                if (resultadoStart == Constants.ResultCode.DP_SUCCESS)
                {
                    Debug.WriteLine("[LOG Biometric SUCCESS] CaptureAsync (re)armado. El sensor está escuchando.");
                    OnEstadoCambiado?.Invoke($"Lector listo ({lector.Description.SerialNumber}). Coloque su dedo.");
                    return true;
                }

                if (resultadoStart == Constants.ResultCode.DP_DEVICE_FAILURE)
                {
                    Debug.WriteLine("[LOG Biometric ERROR] ¡El lector fue desconectado!");
                    OnEstadoCambiado?.Invoke("Se ha desconectado el lector biométrico. Cerrando sistema...");

                    System.Threading.Thread.Sleep(1500);
                    Environment.Exit(0);
                }

                // Fallback a formato ISO si el lector rechaza ANSI
                Debug.WriteLine($"[LOG Biometric ERROR] Falló CaptureAsync ANSI ({resultadoStart}). Intentando ISO...");
                resultadoStart = lector.CaptureAsync(
                    Constants.Formats.Fid.ISO,
                    Constants.CaptureProcessing.DP_IMG_PROC_DEFAULT,
                    lector.Capabilities.Resolutions[0]
                );

                if (resultadoStart == Constants.ResultCode.DP_SUCCESS)
                {
                    Debug.WriteLine("[LOG Biometric SUCCESS] CaptureAsync (re)armado (Modo ISO).");
                    OnEstadoCambiado?.Invoke($"Lector listo ({lector.Description.SerialNumber}). Coloque su dedo.");
                    return true;
                }

                Debug.WriteLine($"[LOG Biometric ERROR] Falló CaptureAsync al rearmar. Código final: {resultadoStart}");
                OnEstadoCambiado?.Invoke($"No se pudo rearmar la captura: {resultadoStart}");
                return false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[LOG Biometric EXCEPCIÓN] Error en ArmarSiguienteCaptura: {ex.Message}");
                OnEstadoCambiado?.Invoke($"Excepción al rearmar el lector: {ex.Message}");
                return false;
            }
        }

        public void DetenerLectura()
        {
            if (lector != null)
            {
                try
                {
                    Debug.WriteLine("[LOG Biometric] Deteniendo lectura y cancelando capturas activas...");

                    // Importante: apagar el flag ANTES de cancelar, para que si un
                    // callback llega justo en este momento no vuelva a rearmar la captura.
                    lecturaActiva = false;

                    lector.CancelCapture();

                    if (onCapturedDelegate != null)
                    {
                        lector.On_Captured -= onCapturedDelegate;
                    }

                    lector.Dispose();
                    lector = null;
                    Debug.WriteLine("[LOG Biometric] Recursos del lector liberados correctamente.");
                    OnEstadoCambiado?.Invoke("Lector detenido correctamente");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[LOG Biometric EXCEPCIÓN] Error al detener el lector: {ex.Message}");
                    OnEstadoCambiado?.Invoke($"Error al detener el lector: {ex.Message}");
                }
            }
        }

        private void OnCapturaRealizada(CaptureResult captureResult)
        {
            Debug.WriteLine("\n[LOG SDK] >>> EVENTO FÍSICO DISPARADO: Dedo detectado en el sensor <<<");

            // TODO EL CUERPO va en un try/finally: sin importar por dónde se salga
            // (éxito, error, calidad mala, excepción), al final SIEMPRE hay que volver
            // a armar la captura si el servicio sigue activo. Si esto no se hace,
            // el lector procesa una única huella y después deja de "escuchar".
            try
            {
                if (captureResult == null)
                {
                    Debug.WriteLine("[LOG Biometric ERROR] CaptureResult es NULO.");
                    OnEstadoCambiado?.Invoke("Error durante la captura de la huella.");
                    return;
                }

                if (captureResult.ResultCode == Constants.ResultCode.DP_DEVICE_FAILURE || captureResult.ResultCode == Constants.ResultCode.DP_INVALID_DEVICE)
                {
                    Debug.WriteLine("[LOG Biometric ERROR FATAL] El lector falló o se desconectó. Cerrando app...");
                    OnEstadoCambiado?.Invoke("Lector desconectado o con fallo crítico. Cerrando aplicación...");
                    Application.DoEvents();
                    System.Threading.Thread.Sleep(2000);
                    MessageBox.Show(
        "El lector biométrico se ha desconectado o sufrió un fallo crítico.\nLa aplicación se cerrará.",
        "Error de Hardware",
        MessageBoxButtons.OK,
        MessageBoxIcon.Error
    );
                    Environment.Exit(0); // Cierra todo el proceso de forma limpia
                    return;
                }

                if (captureResult.ResultCode != Constants.ResultCode.DP_SUCCESS)
                {
                    Debug.WriteLine($"[LOG Biometric ERROR] Fallo en resultado de captura. Código: {captureResult.ResultCode}");
                    OnEstadoCambiado?.Invoke("Error durante la captura de la huella.");
                    return;
                }

                if (captureResult.Data == null)
                {
                    Debug.WriteLine("[LOG Biometric WARNING] Captura realizada pero el objeto devuelto es NULO.");
                    OnEstadoCambiado?.Invoke("Huella no capturada correctamente.");
                    return;
                }

                // Convertir la imagen capturada (Fid) a minucias (Fmd)
                Fid fidCapturado = captureResult.Data as Fid;
                if (fidCapturado == null)
                {
                    Debug.WriteLine("[LOG Biometric ERROR] El objeto de captura no se pudo tratar como Fid.");
                    OnEstadoCambiado?.Invoke("Error en el formato de datos del sensor.");
                    return;
                }

                DataResult<Fmd> resultadoFmd = FeatureExtraction.CreateFmdFromFid(fidCapturado, Constants.Formats.Fmd.ANSI);

                if (resultadoFmd.ResultCode != Constants.ResultCode.DP_SUCCESS || resultadoFmd.Data == null)
                {
                    Debug.WriteLine($"[LOG Biometric ERROR] Falló la extracción de minucias. Código: {resultadoFmd.ResultCode}");
                    OnEstadoCambiado?.Invoke("No se pudo extraer minucias con la calidad adecuada.");
                    return;
                }

                Fmd fmdCapturado = resultadoFmd.Data;
                Debug.WriteLine($"[LOG Biometric] Huella procesada correctamente. Bytes: {fmdCapturado.Bytes.Length}, Formato: {fmdCapturado.Format}");
                OnEstadoCambiado?.Invoke("Huella capturada. Procesando...");

                if (esEnrolamiento)
                {
                    Debug.WriteLine("[LOG Biometric] Modo activo: Enrolamiento.");
                    ProcesarEnrolamiento(fmdCapturado);
                    return;
                }

                // Modo fichada continua: anti-rebote para no generar dos fichadas
                // si el usuario deja el dedo apoyado y el lector se rearma rápido.
                if (DateTime.Now - ultimaCapturaProcesada < COOLDOWN_ENTRE_CAPTURAS)
                {
                    Debug.WriteLine("[LOG Biometric] Captura descartada por cooldown anti-rebote.");
                    return;
                }
                ultimaCapturaProcesada = DateTime.Now;

                Debug.WriteLine("[LOG Biometric] Modo activo: Fichada continua / Verificación.");
                OnHuellaCapturada?.Invoke(fmdCapturado);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[LOG Biometric EXCEPCIÓN] Error inesperado procesando captura: {ex.Message}");
                OnEstadoCambiado?.Invoke($"Error inesperado procesando la huella: {ex.Message}");
            }
            finally
            {
                // Rearmar el sensor para la próxima huella, siempre que el servicio
                // siga activo (no se llamó a DetenerLectura). Esto aplica tanto para
                // fichada continua como para el modo enrolamiento propio de este
                // servicio (que necesita varias muestras seguidas).
                if (lecturaActiva)
                {
                    ArmarSiguienteCaptura();
                }
            }
        }

        public void IniciarEnrolamiento()
        {
            esEnrolamiento = true;
            fmdsEnrolamiento.Clear();
            Debug.WriteLine("[LOG Enrolamiento] Modo enrolamiento activado. Se requieren 4 muestras.");
            OnEstadoCambiado?.Invoke($"Modo Enrolamiento: Por favor coloque el dedo en el lector (Muestra 1 de {CAPTURAS_REQUERIDAS_ENROLAMIENTO})");
            OnProgresoEnrolamientoInternal?.Invoke(0);
        }

        public void CancelarEnrolamiento()
        {
            esEnrolamiento = false;
            fmdsEnrolamiento.Clear();
            Debug.WriteLine("[LOG Enrolamiento] Enrolamiento cancelado por el usuario.");
            OnEstadoCambiado?.Invoke("Enrolamiento cancelado");
        }

        private void ProcesarEnrolamiento(Fmd fmd)
        {
            fmdsEnrolamiento.Add(fmd);
            int conteoActual = fmdsEnrolamiento.Count;
            Debug.WriteLine($"[LOG Enrolamiento] Muestra {conteoActual} agregada.");
            OnProgresoEnrolamientoInternal?.Invoke(conteoActual);

            if (conteoActual < CAPTURAS_REQUERIDAS_ENROLAMIENTO)
            {
                OnEstadoCambiado?.Invoke($"Muestra {conteoActual} recibida. Retire el dedo y colóquelo nuevamente ({conteoActual + 1} de {CAPTURAS_REQUERIDAS_ENROLAMIENTO})");
            }
            else
            {
                Debug.WriteLine("[LOG Enrolamiento] Muestras suficientes capturadas.");
                OnEstadoCambiado?.Invoke("Muestras completadas. Generando plantilla biométrica...");
                esEnrolamiento = false;
            }
        }

        public string ObtenerTemplateBase64Resultado()
        {
            if (fmdsEnrolamiento.Count < CAPTURAS_REQUERIDAS_ENROLAMIENTO)
            {
                Debug.WriteLine($"[LOG Enrolamiento ERROR] Muestras insuficientes para generar template ({fmdsEnrolamiento.Count}/{CAPTURAS_REQUERIDAS_ENROLAMIENTO}).");
                OnEstadoCambiado?.Invoke("No hay suficientes capturas para generar el template");
                return null;
            }

            try
            {
                Debug.WriteLine("[LOG Enrolamiento] Consolidando FMD final mediante Enrollment.CreateEnrollmentFmd...");
                DataResult<Fmd> resultadoEnrolamiento = Enrollment.CreateEnrollmentFmd(
                    Constants.Formats.Fmd.ANSI,
                    fmdsEnrolamiento
                );

                if (resultadoEnrolamiento.ResultCode == Constants.ResultCode.DP_SUCCESS && resultadoEnrolamiento.Data != null)
                {
                    byte[] bytesTemplate = resultadoEnrolamiento.Data.Bytes;
                    fmdsEnrolamiento.Clear();
                    string base64 = Convert.ToBase64String(bytesTemplate);
                    Debug.WriteLine($"[LOG Enrolamiento SUCCESS] Template generado con éxito. Tamaño Base64: {base64.Length} caracteres.");
                    return base64;
                }
                else
                {
                    Debug.WriteLine($"[LOG Enrolamiento ERROR] Falló CreateEnrollmentFmd. Código: {resultadoEnrolamiento.ResultCode}");
                    OnEstadoCambiado?.Invoke($"Error al consolidar el enrolamiento: {resultadoEnrolamiento.ResultCode}");
                    return null;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[LOG Enrolamiento EXCEPCIÓN] Error al generar template: {ex.Message}");
                OnEstadoCambiado?.Invoke($"Excepción al generar el template: {ex.Message}");
                return null;
            }
        }

        public int? IdentificarEmpleado(Fmd huellaCapturada, IEnumerable<Huella> huellasCargadas)
        {
            if (huellaCapturada == null || huellasCargadas == null)
            {
                Debug.WriteLine("[LOG Identificación WARNING] Objeto FMD o lista de huellas nulo.");
                return null;
            }

            const int UMBRAL_FAR = 21474;
            int huellasEvaluadas = 0;

            foreach (var huella in huellasCargadas)
            {
                if (string.IsNullOrEmpty(huella.TemplateBiometrico) || huella.TemplateBiometrico.Length < 100)
                {
                    Debug.WriteLine($"[LOG Identificación SKIP] Huella ID {huella.Id} ignorada por template inválido o demasiado corto.");
                    continue;
                }

                huellasEvaluadas++;
                try
                {
                    byte[] bytesBD = Convert.FromBase64String(huella.TemplateBiometrico);
                    DataResult<Fmd> resultadoImport = null;

                    // Intento 1: Importar directamente como binario ANSI puro (para las nuevas huellas)
                    resultadoImport = Importer.ImportFmd(bytesBD, Constants.Formats.Fmd.ANSI, Constants.Formats.Fmd.ANSI);

                    // Intento 2: Si falla, verificar si era el formato XML histórico
                    if (resultadoImport.ResultCode != Constants.ResultCode.DP_SUCCESS || resultadoImport.Data == null)
                    {
                        try
                        {
                            string posiblesXml = System.Text.Encoding.UTF8.GetString(bytesBD);
                            if (posiblesXml.Contains("<FMD>"))
                            {
                                Fmd fmdXml = Fmd.DeserializeXml(posiblesXml);
                                if (fmdXml != null)
                                {
                                    resultadoImport = new DataResult<Fmd>(Constants.ResultCode.DP_SUCCESS, fmdXml);
                                }
                            }
                        }
                        catch { }
                    }

                    // Intento 3: Fallback usando formato ISO por si acaso
                    if (resultadoImport == null || resultadoImport.ResultCode != Constants.ResultCode.DP_SUCCESS || resultadoImport.Data == null)
                    {
                        resultadoImport = Importer.ImportFmd(bytesBD, Constants.Formats.Fmd.ISO, Constants.Formats.Fmd.ANSI);
                    }

                    if (resultadoImport.ResultCode == Constants.ResultCode.DP_SUCCESS && resultadoImport.Data != null)
                    {
                        Fmd fmdBD = resultadoImport.Data;
                        CompareResult resultado = Comparison.Compare(huellaCapturada, 0, fmdBD, 0);

                        if (resultado.ResultCode == Constants.ResultCode.DP_SUCCESS && resultado.Score < UMBRAL_FAR)
                        {
                            Debug.WriteLine($"[LOG Identificación SUCCESS] ¡MATCH ENCONTRADO! Empleado ID: {huella.EmpleadoId} (Score: {resultado.Score})");
                            OnEmpleadoIdentificado?.Invoke(huella.EmpleadoId);
                            return huella.EmpleadoId;
                        }
                    }
                    else
                    {
                        Debug.WriteLine($"[LOG Identificación ERROR] No se pudo deserializar la huella ID {huella.Id} de la DB. Código: {resultadoImport.ResultCode}");
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[LOG Identificación EXCEPCIÓN] Falló la comparación en Huella ID {huella.Id}: {ex.Message}");
                }
            }

            Debug.WriteLine($"[LOG Identificación FAIL] Se evaluaron {huellasEvaluadas} huellas sin coincidencia.");
            OnHuellaNoReconocida?.Invoke();
            return null;
        }

        public bool VerificarHuella(Fmd huellaCapturada, string templateBase64)
        {
            if (huellaCapturada == null || string.IsNullOrEmpty(templateBase64) || templateBase64.Length < 100)
                return false;

            try
            {
                byte[] bytesTemplate = Convert.FromBase64String(templateBase64);
                DataResult<Fmd> resultadoImport = null;

                resultadoImport = Importer.ImportFmd(bytesTemplate, Constants.Formats.Fmd.ANSI, Constants.Formats.Fmd.ANSI);

                if (resultadoImport.ResultCode != Constants.ResultCode.DP_SUCCESS || resultadoImport.Data == null)
                {
                    try
                    {
                        string posiblesXml = System.Text.Encoding.UTF8.GetString(bytesTemplate);
                        if (posiblesXml.Contains("<FMD>"))
                        {
                            Fmd fmdXml = Fmd.DeserializeXml(posiblesXml);
                            if (fmdXml != null)
                            {
                                resultadoImport = new DataResult<Fmd>(Constants.ResultCode.DP_SUCCESS, fmdXml);
                            }
                        }
                    }
                    catch { }
                }

                if (resultadoImport == null || resultadoImport.ResultCode != Constants.ResultCode.DP_SUCCESS || resultadoImport.Data == null)
                {
                    resultadoImport = Importer.ImportFmd(bytesTemplate, Constants.Formats.Fmd.ISO, Constants.Formats.Fmd.ANSI);
                }

                if (resultadoImport.ResultCode == Constants.ResultCode.DP_SUCCESS && resultadoImport.Data != null)
                {
                    Fmd fmdAlmacenado = resultadoImport.Data;
                    CompareResult resultadoComparacion = Comparison.Compare(huellaCapturada, 0, fmdAlmacenado, 0);

                    const int UMBRAL_FAR = 21474;
                    bool esMatch = resultadoComparacion.ResultCode == Constants.ResultCode.DP_SUCCESS && resultadoComparacion.Score < UMBRAL_FAR;
                    Debug.WriteLine($"[LOG Comparación] Verification score: {resultadoComparacion.Score} | Es Match: {esMatch}");
                    return esMatch;
                }

                return false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[LOG Comparación EXCEPCIÓN]: {ex.Message}");
                return false;
            }
        }


    }
}