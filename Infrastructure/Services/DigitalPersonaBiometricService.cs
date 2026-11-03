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

                enrolando = false;
                muestrasEnrolamiento = null;
                templateEnrolamiento = null;
                leyendo = true;
                if (!AsegurarLectorAbierto())
                {
                    return false;
                }

                PublicarEstado("Lector DigitalPersona listo: " + reader.Description.Name);
                return ProgramarCaptura();
            }
        }

        public bool ReintentarConexion()
        {
            lock (sync)
            {
                if (liberado)
                {
                    throw new ObjectDisposedException(nameof(DigitalPersonaBiometricService));
                }

                CerrarLector(false);
                if (!AsegurarLectorAbierto())
                {
                    return false;
                }

                return !leyendo && !enrolando || ProgramarCaptura();
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

                leyendo = false;
                enrolando = true;
                muestrasEnrolamiento = new List<Fmd>(CapturasEnrolamientoRequeridas);
                templateEnrolamiento = null;
                if (!AsegurarLectorAbierto())
                {
                    return;
                }

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
            if (huellaCapturada == null)
            {
                return false;
            }

            ResultadoImportacionTemplate importada = TemplateBiometricoImporter.TryImportarTemplateBiometrico(templateBase64);
            if (!importada.Exito || importada.Fmd == null)
            {
                return false;
            }

            Fmd almacenada = importada.Fmd;
            CompareResult comparacion = Comparison.Compare(huellaCapturada, 0, almacenada, 0);
            if (comparacion.ResultCode != Constants.ResultCode.DP_SUCCESS)
            {
                throw new InvalidOperationException("DigitalPersona no pudo comparar las huellas: " + comparacion.ResultCode);
            }

            return comparacion.Score < UmbralIdentificacion;
        }

        public int? IdentificarEmpleado(Fmd huellaCapturada, IEnumerable<HuellaEnMemoria> huellasCargadas)
        {
            if (huellaCapturada == null || huellasCargadas == null)
            {
                PublicarNoReconocida();
                return null;
            }

            var galeria = new List<Fmd>();
            var empleadosPorIndice = new List<int>();
            foreach (HuellaEnMemoria huella in huellasCargadas)
            {
                if (huella == null || huella.Plantilla == null || huella.EmpleadoId <= 0)
                {
                    continue;
                }

                galeria.Add(huella.Plantilla);
                empleadosPorIndice.Add(huella.EmpleadoId);
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
                PublicarEstado("DigitalPersona no pudo identificar la huella: " + identificacion.ResultCode);
                PublicarNoReconocida();
                return null;
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
            CerrarLector(true);
            lock (sync)
            {
                liberado = true;
            }
        }

        private bool AsegurarLectorAbierto()
        {
            if (reader != null)
            {
                try
                {
                    if (reader.GetStatus() == Constants.ResultCode.DP_SUCCESS &&
                        (reader.Status.Status == Constants.ReaderStatuses.DP_STATUS_READY ||
                         reader.Status.Status == Constants.ReaderStatuses.DP_STATUS_NEED_CALIBRATION))
                    {
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("[Biometría] No se pudo consultar el lector abierto: " + ex.Message);
                }

                CerrarLector(false);
            }
            return AbrirLectorInterno();
        }

        private bool AbrirLectorInterno()
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
                    CerrarLector(false);
                    return false;
                }

                ConfiguracionLocalDto configuracion = DispositivoConfigService.LeerConfiguracion();
                string serialGuardado = configuracion == null ? null : configuracion.SerialLector;
                reader = SeleccionLector.PorSerial(
                    readers.Cast<Reader>(),
                    serialGuardado,
                    item => item.Description.SerialNumber);

                if (reader == null)
                {
                    PublicarEstado("El lector guardado no está conectado. Verifique que sea el mismo U.are.U 4500.");
                    CerrarLector(false);
                    return false;
                }

                Constants.ResultCode apertura = reader.Open(Constants.CapturePriority.DP_PRIORITY_EXCLUSIVE);
                if (apertura != Constants.ResultCode.DP_SUCCESS)
                {
                    apertura = reader.Open(Constants.CapturePriority.DP_PRIORITY_COOPERATIVE);
                }
                if (apertura != Constants.ResultCode.DP_SUCCESS)
                {
                    PublicarEstado("No se pudo abrir el lector DigitalPersona: " + apertura);
                    CerrarLector(false);
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
                CerrarLector(false);
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
                    CerrarLector(false);
                    return false;
                }

                if (reader.Status.Status == Constants.ReaderStatuses.DP_STATUS_NEED_CALIBRATION)
                {
                    Constants.ResultCode calibracion = reader.Calibrate();
                    if (calibracion != Constants.ResultCode.DP_SUCCESS)
                    {
                        PublicarEstado("No se pudo calibrar el lector DigitalPersona: " + calibracion);
                        CerrarLector(false);
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
                else if (reader.Status.Status == Constants.ReaderStatuses.DP_STATUS_FAILURE)
                {
                    PublicarEstado("El lector DigitalPersona reportó fallo.");
                    CerrarLector(false);
                    return false;
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
                    CerrarLector(false);
                    return false;
                }

                capturaEnCurso = true;
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[Biometría] Error al iniciar captura: " + ex);
                PublicarEstado("Error al iniciar captura biométrica: " + ex.Message);
                CerrarLector(false);
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
                if (!enModoEnrolamiento)
                {
                    PublicarNoReconocida();
                }

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

        private void CerrarLector(bool detenerOperacion)
        {
            lock (sync)
            {
                if (detenerOperacion)
                {
                    leyendo = false;
                    enrolando = false;
                    muestrasEnrolamiento = null;
                }

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
