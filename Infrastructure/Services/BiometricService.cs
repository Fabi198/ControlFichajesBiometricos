using DevsFingerPrint.Domain.Interfaces;
using DevsFingerPrint.Domain.Models;
using DPUruNet;
using System;
using System.CodeDom;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DevsFingerPrint.Infrastructure.Services
{
    public class BiometricService : IBiometricService
    {

        private Reader lector;
        private bool esEnrolamiento;
        private List<Fmd> fmdsEnrolamiento;
        private const int CAPTURAS_REQUERIDAS_ENROLAMIENTO = 4;

        // Events de la interfaz
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
        }

        // Control del lector USB

        public bool IniciarLectura()
        {
            try
            {
                ReaderCollection listaLectores = ReaderCollection.GetReaders();

                if (listaLectores.Count == 0)
                {
                    OnEstadoCambiado?.Invoke("No se detecto ningun lector biometrico conectado");
                    return false;
                }

                lector = listaLectores[0];


                Constants.ResultCode resultado = lector.Open(Constants.CapturePriority.DP_PRIORITY_COOPERATIVE);
                if (resultado != Constants.ResultCode.DP_SUCCESS)
                {
                    OnEstadoCambiado?.Invoke($"Error al abrir el lector: {resultado}");
                    return false;
                }

                lector.On_Captured += OnCapturaRealizada;

                Constants.ResultCode resultadoCapture = lector.CaptureAsync(
                    Constants.Formats.Fid.ANSI,
                    Constants.CaptureProcessing.DP_IMG_PROC_DEFAULT,
                    500
                    );

                if (resultadoCapture == Constants.ResultCode.DP_SUCCESS)
                {
                    OnEstadoCambiado?.Invoke($"Lector listo ({lector.Description.SerialNumber}). Coloque su dedo.");
                    return true;
                }
                else
                {
                    OnEstadoCambiado?.Invoke($"No se pudo iniciar la captura: {resultadoCapture}");
                    return false;
                }
            } catch (Exception ex)
            {
                OnEstadoCambiado?.Invoke($"Excepcion al iniciar lector: {ex.Message}");
                return false;
            }
        }

        public void DetenerLectura()
        {
            if (lector != null)
            {
                try
                {
                    lector.CancelCapture();
                    lector.On_Captured -= OnCapturaRealizada;
                    lector.Dispose();
                    lector = null;
                    OnEstadoCambiado?.Invoke("Lector detenido correctamente");
                }
                catch (Exception ex)
                {
                    OnEstadoCambiado?.Invoke($"Error al detener el lector: {ex.Message}");
                }
            }
        }


        // Eventos de captura y procesos

        private void OnCapturaRealizada(CaptureResult captureResult)
        {

            if (captureResult.ResultCode != Constants.ResultCode.DP_SUCCESS || captureResult.Data == null)
            {
                OnEstadoCambiado?.Invoke("Error durante la captura de la huella");
            }

            OnEstadoCambiado?.Invoke("Huella capturada. Procesando...");

            Fid fid = captureResult.Data;


            DataResult<Fmd> resultadoFmd = FeatureExtraction.CreateFmdFromFid(fid, Constants.Formats.Fmd.ANSI);

            if (resultadoFmd.ResultCode != Constants.ResultCode.DP_SUCCESS || resultadoFmd.Data == null)
            {
                OnEstadoCambiado?.Invoke("No se pudo extraer minucias con la calidad adecuada. Intente de nuevo");
                return;
            }

            Fmd fmdCapturado = resultadoFmd.Data;

            if (esEnrolamiento)
            {
                ProcesarEnrolamiento(fmdCapturado);
            } else
            {
                OnHuellaCapturada?.Invoke(fmdCapturado);
            }
        }

        // Enrolamiento

        public void IniciarEnrolamiento()
        {
            esEnrolamiento = true;
            fmdsEnrolamiento.Clear();
            OnEstadoCambiado?.Invoke($"Modo Enrolamiento: Por favor coloque el dedo en el lector (Muestra 1 de {CAPTURAS_REQUERIDAS_ENROLAMIENTO}");
            OnProgresoEnrolamientoInternal?.Invoke(0);
        }

        public void CancelarEnrolamiento()
        {
            esEnrolamiento = false;
            fmdsEnrolamiento.Clear();
            OnEstadoCambiado?.Invoke("Enrolamiento cancelado");
        }

        private void ProcesarEnrolamiento(Fmd fmd)
        {
            fmdsEnrolamiento.Add(fmd);
            int conteoActual = fmdsEnrolamiento.Count;
            OnProgresoEnrolamientoInternal?.Invoke(conteoActual);

            if(conteoActual < CAPTURAS_REQUERIDAS_ENROLAMIENTO)
            {
                OnEstadoCambiado?.Invoke($"Muestra {conteoActual} recibida. Retire el dedo y coloquelo nuevamente ({conteoActual + 1} de {CAPTURAS_REQUERIDAS_ENROLAMIENTO}");
            } else
            {
                OnEstadoCambiado?.Invoke("Muestras completadas. Generando plantilla biometrica...");
                esEnrolamiento = false;
            }
        }

        public string ObtenerTemplateBase64Resultado()
        {
            if (fmdsEnrolamiento.Count < CAPTURAS_REQUERIDAS_ENROLAMIENTO)
            {
                OnEstadoCambiado?.Invoke("No hay suficientes capturas para generar el template");
                return null;
            }

            try
            {
                DataResult<Fmd> resultadoEnrolamiento = Enrollment.CreateEnrollmentFmd(
                    Constants.Formats.Fmd.ANSI,
                    fmdsEnrolamiento
                    );

                if (resultadoEnrolamiento.ResultCode == Constants.ResultCode.DP_SUCCESS && resultadoEnrolamiento.Data != null)
                {
                    byte[] bytesTemplate = resultadoEnrolamiento.Data.Bytes;
                    fmdsEnrolamiento.Clear();
                    return Convert.ToBase64String(bytesTemplate);
                } else
                {
                    OnEstadoCambiado?.Invoke($"Error al consolidar el enrolamiento: {resultadoEnrolamiento.ResultCode}");
                    return null;
                }
            } catch (Exception ex)
            {
                OnEstadoCambiado?.Invoke($"Excepcion al generar el template: {ex.Message}");
                return null;
            }
        }


        // Verificacion e Identificacion

        public bool VerificarHuella(Fmd huellaCapturada, string templateBase64)
        {
            if (huellaCapturada == null || string.IsNullOrEmpty(templateBase64))
                return false;


            try
            {
                byte[] bytesTemplate = Convert.FromBase64String(templateBase64);

                DataResult<Fmd> resultadoImport = Importer.ImportFmd(bytesTemplate, Constants.Formats.Fmd.ANSI, Constants.Formats.Fmd.ANSI);

                if (resultadoImport.ResultCode == Constants.ResultCode.DP_SUCCESS)
                {
                    Fmd fmdAlmacenado = resultadoImport.Data;


                    CompareResult resultadoComparacion = Comparison.Compare(
                        huellaCapturada, 0,
                        fmdAlmacenado, 0
                        );

                    const int UMBRAL_FAR = 2147;
                    return resultadoComparacion.ResultCode == Constants.ResultCode.DP_SUCCESS && resultadoComparacion.Score < UMBRAL_FAR;
                }else
                {
                    OnEstadoCambiado?.Invoke("Error al importar el fmd");
                    return false;
                }
            } catch (Exception ex)
            {
                return false;
            }

        }


        public int? IdentificarEmpleado(Fmd huellaCapturada, IEnumerable<Huella> huellasCargadas)
        {
            if (huellaCapturada == null || huellasCargadas == null)
                return null;

            const int UMBRAL_FAR = 2147;

            foreach (var huella in huellasCargadas)
            {
                try
                {
                    byte[] bytesBD = Convert.FromBase64String(huella.TemplateBiometrico);
                    string fmdXml = System.Text.Encoding.UTF8.GetString(bytesBD);
                    Fmd fmdBD = Fmd.DeserializeXml(fmdXml);


                    CompareResult resultado = Comparison.Compare(huellaCapturada, 0, fmdBD, 0);

                    if (resultado.ResultCode == Constants.ResultCode.DP_SUCCESS && resultado.Score < UMBRAL_FAR)
                    {
                        OnEmpleadoIdentificado?.Invoke(huella.EmpleadoId);
                        return huella.EmpleadoId;
                    }
                } catch
                {
                    continue;
                }
            }

            OnHuellaNoReconocida?.Invoke();
            return null;
        }

    }
}
