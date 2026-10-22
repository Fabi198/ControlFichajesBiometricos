using DevsFingerPrint.Domain.Interfaces;
using DevsFingerPrint.Domain.Models;
using DPUruNet;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DevsFingerPrint.Infrastructure.Services
{
    public sealed class DigitalPersonaBiometricService : IBiometricService, IDisposable
    {
        private const int CapturasEnrolamientoRequeridas = 4;
        private const int UmbralIdentificacion = 0x7fffffff / 100000;
        private readonly object sync = new object();
        private readonly SynchronizationContext synchronizationContext;
        private ReaderCollection readers;
        private Reader reader;
        private bool leyendo;
        private bool enrolando;
        private bool capturaEnCurso;
        private bool liberado;
        private List<Fmd> muestrasEnrolamiento;
        private string templateEnrolamiento;

        public event Action<Fmd> OnHuellaCapturada;
        public event Action<int> OnEmpleadoIdentificado;
        public event Action OnHuellaNoReconocida;
        public event Action<string> OnEstadoCambiado;
        public event Action<int> OnProgresoEnrolamiento;

        public DigitalPersonaBiometricService()
        {
            synchronizationContext = SynchronizationContext.Current ?? new WindowsFormsSynchronizationContext();
        }

        public Reader ObtenerLectorActual()
        {
            lock (sync)
            {
                return reader;
            }
        }

        public bool IniciarLectura()
        {
            lock (sync)
            {
                if (liberado)
                {
                    throw new ObjectDisposedException(nameof(DigitalPersonaBiometricService));
                }

                if (!AsegurarLectorAbierto())
                {
                    return false;
                }

                enrolando = false;
                muestrasEnrolamiento = null;
                templateEnrolamiento = null;
                leyendo = true;
                PublicarEstado("Lector DigitalPersona listo: " + reader.Description.Name);
                return ProgramarCaptura();
            }
        }

        public void DetenerLectura()
        {
            lock (sync)
            {
                leyendo = false;
                capturaEnCurso = false;
                if (reader != null)
                {
                    try
                    {
                        reader.CancelCapture();
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine("[Biometría] Error al cancelar captura: " + ex.Message);
                    }
                }
            }

            PublicarEstado("Lectura biométrica detenida.");
        }

        public void IniciarEnrolamiento()
        {
            lock (sync)
            {
                if (liberado)
                {
                    throw new ObjectDisposedException(nameof(DigitalPersonaBiometricService));
                }

                if (!AsegurarLectorAbierto())
                {
                    return;
                }

                leyendo = false;
                enrolando = true;
                muestrasEnrolamiento = new List<Fmd>(CapturasEnrolamientoRequeridas);
                templateEnrolamiento = null;
                PublicarProgreso(0);
                PublicarEstado("Enrolamiento listo. Coloque el mismo dedo en el lector.");
                ProgramarCaptura();
            }
        }

        public void CancelarEnrolamiento()
        {
            lock (sync)
            {
                enrolando = false;
                muestrasEnrolamiento = null;
                templateEnrolamiento = null;
                capturaEnCurso = false;
                if (reader != null)
                {
                    try
                    {
                        reader.CancelCapture();
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine("[Biometría] Error al cancelar captura de enrolamiento: " + ex.Message);
                    }
                }
            }

            PublicarEstado("Enrolamiento cancelado.");
        }

        public string ObtenerTemplateBase64Resultado()
        {
            lock (sync)
            {
                if (string.IsNullOrEmpty(templateEnrolamiento))
                {
                    return null;
                }

                string resultado = templateEnrolamiento;
                templateEnrolamiento = null;
                muestrasEnrolamiento = null;
                enrolando = false;
                return resultado;
            }
        }

        public bool VerificarHuella(Fmd huellaCapturada, string templateBase64)
        {
            if (huellaCapturada == null || string.IsNullOrWhiteSpace(templateBase64))
            {
                return false;
            }

            Fmd almacenada = DeserializarTemplate(templateBase64);
            CompareResult comparacion = Comparison.Compare(huellaCapturada, 0, almacenada, 0);
            if (comparacion.ResultCode != Constants.ResultCode.DP_SUCCESS)
            {
                throw new InvalidOperationException("DigitalPersona no pudo comparar las huellas: " + comparacion.ResultCode);
            }

            return comparacion.Score < UmbralIdentificacion;
        }

        public int? IdentificarEmpleado(Fmd huellaCapturada, IEnumerable<Huella> huellasCargadas)
        {
            if (huellaCapturada == null || huellasCargadas == null)
            {
                PublicarNoReconocida();
                return null;
            }

            var galeria = new List<Fmd>();
            var empleadosPorIndice = new List<int>();
            foreach (Huella huella in huellasCargadas)
            {
                if (huella == null || string.IsNullOrWhiteSpace(huella.TemplateBiometrico))
                {
                    continue;
                }

                try
                {
                    galeria.Add(DeserializarTemplate(huella.TemplateBiometrico));
                    empleadosPorIndice.Add(huella.EmpleadoId);
                }
                catch (Exception ex) when (
                    ex is FormatException ||
                    ex is ArgumentException ||
                    ex is SDKException)
                {
                    Debug.WriteLine("[Biometría] Se omitió un template inválido del catálogo local: " + ex.Message);
                    PublicarEstado("Hay una huella almacenada inválida; sincronice el catálogo antes de fichar.");
                }
            }

            if (galeria.Count == 0)
            {
                PublicarNoReconocida();
                return null;
            }

            IdentifyResult identificacion = Comparison.Identify(
                huellaCapturada,
                0,
                galeria,
                UmbralIdentificacion,
                2);

            if (identificacion.ResultCode != Constants.ResultCode.DP_SUCCESS)
            {
                throw new InvalidOperationException("DigitalPersona no pudo identificar la huella: " + identificacion.ResultCode);
            }

            if (identificacion.Indexes == null || identificacion.Indexes.Length == 0)
            {
                PublicarNoReconocida();
                return null;
            }

            if (identificacion.Indexes.Length > 1)
            {
                PublicarEstado("Coincidencia biométrica ambigua. No se registró la fichada.");
                PublicarNoReconocida();
                return null;
            }

            int indiceEmpleado = identificacion.Indexes[0][0];
            int empleadoId = empleadosPorIndice[indiceEmpleado];
            PublicarIdentificado(empleadoId);
            return empleadoId;
        }

        public void Dispose()
        {
            CerrarLector();
            lock (sync)
            {
                liberado = true;
            }
        }

        private bool AsegurarLectorAbierto()
        {
            if (reader != null)
            {
                return true;
            }

            try
            {
                readers = ReaderCollection.GetReaders();
                if (readers == null || readers.Count == 0)
                {
                    PublicarEstado("No se detectó un lector DigitalPersona. Conecte el U.are.U 4500 y vuelva a iniciar.");
                    CerrarLector();
                    return false;
                }

                ConfiguracionLocalDto configuracion = DispositivoConfigService.LeerConfiguracion();
                string serialGuardado = configuracion == null ? null : configuracion.SerialLector;
                reader = string.IsNullOrWhiteSpace(serialGuardado)
                    ? readers[0]
                    : readers.Cast<Reader>().FirstOrDefault(item =>
                        string.Equals(item.Description.SerialNumber, serialGuardado, StringComparison.OrdinalIgnoreCase));

                if (reader == null && readers.Count > 0)
                {
                    reader = readers[0];
                }

                if (reader == null)
                {
                    PublicarEstado("El lector guardado no está conectado. Verifique el U.are.U 4500.");
                    CerrarLector();
                    return false;
                }

                Constants.ResultCode apertura = reader.Open(Constants.CapturePriority.DP_PRIORITY_COOPERATIVE);
                if (apertura != Constants.ResultCode.DP_SUCCESS)
                {
                    PublicarEstado("No se pudo abrir el lector DigitalPersona: " + apertura);
                    CerrarLector();
                    return false;
                }

                if (string.IsNullOrWhiteSpace(serialGuardado))
                {
                    DispositivoConfigService.GuardarConfiguracion(
                        configuracion == null ? 0 : configuracion.SucursalId,
                        reader.Description.SerialNumber);
                }

                reader.On_Captured += ReaderOnCaptured;
                PublicarEstado("Lector conectado: " + reader.Description.Name);
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[Biometría] No se pudo inicializar el lector: " + ex);
                PublicarEstado("Error al inicializar el lector DigitalPersona: " + ex.Message);
                CerrarLector();
                return false;
            }
        }

        private bool ProgramarCaptura()
        {
            if (reader == null || capturaEnCurso || (!leyendo && !enrolando))
            {
                return reader != null;
            }

            try
            {
                Constants.ResultCode estado = reader.GetStatus();
                if (estado != Constants.ResultCode.DP_SUCCESS)
                {
                    PublicarEstado("No se pudo consultar el lector DigitalPersona: " + estado);
                    return false;
                }

                if (reader.Status.Status == Constants.ReaderStatuses.DP_STATUS_NEED_CALIBRATION)
                {
                    Constants.ResultCode calibracion = reader.Calibrate();
                    if (calibracion != Constants.ResultCode.DP_SUCCESS)
                    {
                        PublicarEstado("No se pudo calibrar el lector DigitalPersona: " + calibracion);
                        return false;
                    }
                }
                else if (reader.Status.Status == Constants.ReaderStatuses.DP_STATUS_BUSY)
                {
                    Task.Delay(100).ContinueWith(_ =>
                    {
                        lock (sync)
                        {
                            if (leyendo || enrolando)
                            {
                                ProgramarCaptura();
                            }
                        }
                    });
                    return true;
                }
                else if (reader.Status.Status != Constants.ReaderStatuses.DP_STATUS_READY)
                {
                    PublicarEstado("El lector DigitalPersona no está listo: " + reader.Status.Status);
                    return false;
                }

                int resolucion = reader.Capabilities.Resolutions == null || reader.Capabilities.Resolutions.Length == 0
                    ? 500
                    : reader.Capabilities.Resolutions[0];
                Constants.ResultCode inicio = reader.CaptureAsync(
                    Constants.Formats.Fid.ANSI,
                    Constants.CaptureProcessing.DP_IMG_PROC_DEFAULT,
                    resolucion);
                if (inicio != Constants.ResultCode.DP_SUCCESS)
                {
                    PublicarEstado("No se pudo iniciar la captura biométrica: " + inicio);
                    return false;
                }

                capturaEnCurso = true;
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[Biometría] Error al iniciar captura: " + ex);
                PublicarEstado("Error al iniciar captura biométrica: " + ex.Message);
                return false;
            }
        }

        private void ReaderOnCaptured(CaptureResult resultado)
        {
            lock (sync)
            {
                capturaEnCurso = false;
                if (!leyendo && !enrolando)
                {
                    return;
                }
            }

            try
            {
                if (resultado.ResultCode != Constants.ResultCode.DP_SUCCESS || resultado.Data == null)
                {
                    if (resultado.Quality != Constants.CaptureQuality.DP_QUALITY_CANCELED)
                    {
                        PublicarEstado("La captura del lector falló: " + resultado.ResultCode + " (" + resultado.Quality + ").");
                    }
                }
                else
                {
                    ProcesarCaptura(resultado.Data);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[Biometría] Error procesando captura: " + ex);
                PublicarEstado("No se pudo procesar la huella: " + ex.Message);
            }
            finally
            {
                lock (sync)
                {
                    if (leyendo || enrolando)
                    {
                        ProgramarCaptura();
                    }
                }
            }
        }

        private void ProcesarCaptura(Fid fid)
        {
            bool enModoEnrolamiento;
            lock (sync)
            {
                enModoEnrolamiento = enrolando;
            }

            DataResult<Fmd> extraccion = FeatureExtraction.CreateFmdFromFid(
                fid,
                Constants.Formats.Fmd.ANSI);
            if (extraccion.ResultCode != Constants.ResultCode.DP_SUCCESS || extraccion.Data == null)
            {
                if (extraccion.ResultCode != Constants.ResultCode.DP_TOO_SMALL_AREA)
                {
                    PublicarEstado("No se pudieron extraer características de la huella: " + extraccion.ResultCode);
                }

                return;
            }

            if (!enModoEnrolamiento)
            {
                PublicarHuellaCapturada(extraccion.Data);
                return;
            }

            lock (sync)
            {
                if (!enrolando || muestrasEnrolamiento == null)
                {
                    return;
                }

                muestrasEnrolamiento.Add(extraccion.Data);
                int progreso = muestrasEnrolamiento.Count;
                if (progreso < CapturasEnrolamientoRequeridas)
                {
                    PublicarProgreso(progreso);
                    PublicarEstado("Muestra " + progreso + " de " + CapturasEnrolamientoRequeridas + " capturada. Levante y vuelva a apoyar el mismo dedo.");
                    return;
                }

                DataResult<Fmd> enrolamiento = Enrollment.CreateEnrollmentFmd(
                    Constants.Formats.Fmd.ANSI,
                    muestrasEnrolamiento);
                if (enrolamiento.ResultCode != Constants.ResultCode.DP_SUCCESS || enrolamiento.Data == null)
                {
                    muestrasEnrolamiento.Clear();
                    PublicarProgreso(0);
                    PublicarEstado("El enrolamiento no fue válido. Vuelva a intentar las cuatro muestras: " + enrolamiento.ResultCode);
                    return;
                }

                templateEnrolamiento = Convert.ToBase64String(
                    Encoding.UTF8.GetBytes(Fmd.SerializeXml(enrolamiento.Data)));
                enrolando = false;
                PublicarProgreso(progreso);
                PublicarEstado("Enrolamiento biométrico completado.");
            }
        }

        private static Fmd DeserializarTemplate(string templateBase64)
        {
            byte[] contenido = Convert.FromBase64String(templateBase64);
            string xml = Encoding.UTF8.GetString(contenido);
            Fmd fmd = Fmd.DeserializeXml(xml);
            if (fmd == null || fmd.ViewCount == 0)
            {
                throw new FormatException("El template biométrico está vacío o no es válido.");
            }

            return fmd;
        }

        private void CerrarLector()
        {
            lock (sync)
            {
                leyendo = false;
                enrolando = false;
                muestrasEnrolamiento = null;
                capturaEnCurso = false;
                if (reader != null)
                {
                    try
                    {
                        reader.On_Captured -= ReaderOnCaptured;
                        reader.CancelCapture();
                        reader.Dispose();
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine("[Biometría] Error liberando lector: " + ex.Message);
                    }

                    reader = null;
                }

                // DPUruNet ReaderCollection.Dispose() unloads the native driver subsystem
                // for the current process, causing future GetReaders() calls to return 0 readers.
                readers = null;
            }
        }

        private void PublicarEstado(string mensaje)
        {
            Publicar(() => OnEstadoCambiado?.Invoke(mensaje));
        }

        private void PublicarProgreso(int progreso)
        {
            Publicar(() => OnProgresoEnrolamiento?.Invoke(progreso));
        }

        private void PublicarHuellaCapturada(Fmd fmd)
        {
            Publicar(() => OnHuellaCapturada?.Invoke(fmd));
        }

        private void PublicarIdentificado(int empleadoId)
        {
            Publicar(() => OnEmpleadoIdentificado?.Invoke(empleadoId));
        }

        private void PublicarNoReconocida()
        {
            Publicar(() => OnHuellaNoReconocida?.Invoke());
        }

        private void Publicar(Action callback)
        {
            if (synchronizationContext == null || SynchronizationContext.Current == synchronizationContext)
            {
                callback();
            }
            else
            {
                synchronizationContext.Post(_ => callback(), null);
            }
        }
    }
}
