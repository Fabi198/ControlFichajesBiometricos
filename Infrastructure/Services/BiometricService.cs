using DevsFingerPrint.Domain.Interfaces;
using DevsFingerPrint.Domain.Models;
using DPUruNet;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace DevsFingerPrint.Infrastructure.Services
{
    public class BiometricService : IBiometricService
    {
        private bool esEnrolamiento;
        private int muestrasEnrolamientoActual = 0;
        private const int CAPTURAS_REQUERIDAS_ENROLAMIENTO = 4;
        private readonly ApiClient _apiClient;

        // Eventos requeridos por la interfaz
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

        public BiometricService(ApiClient apiClient)
        {
            _apiClient = apiClient;
            Debug.WriteLine("[LOG MockBiometricService] Instancia creada.");
        }

        public Reader ObtenerLectorActual()
        {
            // Como es un mock y no hay lector físico, retornamos null o manejamos lógica simulada
            Debug.WriteLine("[LOG MockBiometric] ObtenerLectorActual llamado (Mock).");
            return null;
        }

        public bool IniciarLectura()
        {
            Debug.WriteLine("[LOG MockBiometric] Lector simulado iniciado correctamente.");
            OnEstadoCambiado?.Invoke("Lector Mock listo. Use los botones de simulación.");
            return true;
        }

        public void DetenerLectura()
        {
            Debug.WriteLine("[LOG MockBiometric] Lector simulado detenido.");
            OnEstadoCambiado?.Invoke("Lector Mock detenido.");
        }

        public void IniciarEnrolamiento()
        {
            esEnrolamiento = true;
            muestrasEnrolamientoActual = 0;
            Debug.WriteLine("[LOG MockEnrolamiento Mock] Modo enrolamiento activado.");
            OnEstadoCambiado?.Invoke($"Modo Enrolamiento (Mock): Coloque el dedo (Muestra 1 de {CAPTURAS_REQUERIDAS_ENROLAMIENTO})");
            OnProgresoEnrolamientoInternal?.Invoke(0);
        }

        public void CancelarEnrolamiento()
        {
            esEnrolamiento = false;
            muestrasEnrolamientoActual = 0;
            Debug.WriteLine("[LOG MockEnrolamiento Mock] Enrolamiento cancelado.");
            OnEstadoCambiado?.Invoke("Enrolamiento cancelado (Mock)");
        }

        public string ObtenerTemplateBase64Resultado()
        {
            if (muestrasEnrolamientoActual < CAPTURAS_REQUERIDAS_ENROLAMIENTO)
            {
                Debug.WriteLine("[LOG MockEnrolamiento ERROR] Faltan muestras para completar el enrolamiento mock.");
                return null;
            }

            // Retornamos un Base64 falso pero válido en tamaño para que pase las validaciones de longitud (> 100)
            byte[] dummyBytes = new byte[200];
            new Random().NextBytes(dummyBytes);
            string base64Falso = Convert.ToBase64String(dummyBytes);

            Debug.WriteLine("[LOG MockEnrolamiento SUCCESS] Template mock generado con éxito.");
            muestrasEnrolamientoActual = 0;
            return base64Falso;
        }

        public int? IdentificarEmpleado(Fmd huellaCapturada, IEnumerable<Huella> huellasCargadas)
        {
            Debug.WriteLine("[LOG Mock Identificación] Simulando búsqueda de empleado...");

            // Simulación: Si hay huellas cargadas, retornamos el EmpleadoId de la primera por defecto
            foreach (var huella in huellasCargadas)
            {
                Debug.WriteLine($"[LOG Mock Identificación] Match simulado con Empleado ID: {huella.EmpleadoId}");
                OnEmpleadoIdentificado?.Invoke(huella.EmpleadoId);
                return huella.EmpleadoId;
            }

            OnHuellaNoReconocida?.Invoke();
            return null;
        }

        public bool VerificarHuella(Fmd huellaCapturada, string templateBase64)
        {
            Debug.WriteLine("[LOG Mock Verificación] Verificación simulada exitosa.");
            return true; // Siempre retorna true en el mock para pruebas rápidas
        }

        // ==========================================
        // MÉTODOS AUXILIARES PARA GATILLAR DESDE LA UI
        // ==========================================

        /// <summary>
        /// Simula que el usuario apoyó el dedo en el sensor físico.
        /// Útil para llamarlo desde un botón de prueba en tu interfaz.
        /// </summary>
        public void SimularLecturaDedo(int empleadoIdSimulado = 1)
        {
            Debug.WriteLine("\n[LOG Mock] >>> SIMULACIÓN DE HUELLA DETECTADA <<<");

            if (esEnrolamiento)
            {
                muestrasEnrolamientoActual++;
                OnProgresoEnrolamientoInternal?.Invoke(muestrasEnrolamientoActual);

                if (muestrasEnrolamientoActual < CAPTURAS_REQUERIDAS_ENROLAMIENTO)
                {
                    OnEstadoCambiado?.Invoke($"Muestra {muestrasEnrolamientoActual} recibida (Mock). Coloque el dedo nuevamente.");
                }
                else
                {
                    OnEstadoCambiado?.Invoke("Muestras completadas (Mock). Ya puede obtener el template.");
                    esEnrolamiento = false;
                }
            }
            else
            {
                // Modo fichada: Creamos un Fmd simulado o disparamos el evento de huella capturada
                // Nota: Como instanciar un Fmd real de DPUruNet de cero requiere estructuras nativas complejas, 
                // puedes disparar directamente el evento de identificación si manejas la lógica en la UI o pasar un Fmd nulo si tu UI lo tolera, 
                // o bien notificar el ID directamente simulando que la identificación interna lo resolvió.
                OnEmpleadoIdentificado?.Invoke(empleadoIdSimulado);
            }
        }
    }
}