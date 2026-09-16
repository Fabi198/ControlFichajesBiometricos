using DevsFingerPrint.Domain.Interfaces;
using DevsFingerPrint.Domain.Models;
using DevsFingerPrint.Infrastructure.Data;
using DevsFingerPrint.Infrastructure.Repositories;
using DevsFingerPrint.Infrastructure.Services;
using DPUruNet;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;

namespace DevsFingerPrint.Presentation
{
    public class MainTrayContext : ApplicationContext
    {
        private readonly NotifyIcon notifyIcon;
        private readonly IBiometricService biometricService;
        private readonly IFichadaRepository fichadaRepository;
        private List<Huella> huellasCargadas;
        private ApiClient _apiClient;
        private readonly LectorListenerForm lectorFormularioOculto;
        private System.Windows.Forms.Timer heartbeatTimer;

        private readonly System.Windows.Forms.Timer syncTimer;
        private System.Windows.Forms.Timer arranqueLectorTimer;

        public MainTrayContext(ApiClient apiClient)
        {
            System.Diagnostics.Debug.WriteLine("[LOG MainTray] Inicializando MainTrayContext...");
            LocalDatabase.Inicializar();

            _apiClient = apiClient;
            fichadaRepository = new FichadaRepository();
            biometricService = new BiometricService(_apiClient);


            huellasCargadas = new List<Huella>();



            var contextMenu = new ContextMenuStrip();
            contextMenu.Items.Add("Enrolar nueva huella", null, EnrolarNuevoEmpleado);
            contextMenu.Items.Add("-");
            contextMenu.Items.Add("Estado del Lector", null, MostrarEstadoLector);
            contextMenu.Items.Add("-");
            contextMenu.Items.Add("Salir", null, SalirAplicacion);

            notifyIcon = new NotifyIcon
            {
                Icon = Properties.Resources.circularColor,
                ContextMenuStrip = contextMenu,
                Visible = true,
                Text = "DevsFingerPrint - Control de Fichajes"
            };

            // Eventos del servicio biométrico
            biometricService.OnHuellaCapturada += OnHuellaCapturada;
            biometricService.OnEmpleadoIdentificado += OnEmpleadoIdentificado;
            biometricService.OnHuellaNoReconocida += OnHuellaNoReconocida;
            biometricService.OnEstadoCambiado += OnEstadoCambiado;

            // Temporizador de sincronización periódica (cada 2 horas)
            syncTimer = new System.Windows.Forms.Timer();
            syncTimer.Interval = 60000 * 60 * 2;
            syncTimer.Tick += (s, e) => SincronizarConServidorAsync();

            // 1. Instanciar el formulario invisible y forzar la creación del Handle
            lectorFormularioOculto = new LectorListenerForm();
            lectorFormularioOculto.CreateControl();

            // 2. Cargar huellas en caché si ya existen localmente
            CargarHuellasLocales();

            // 3. Iniciar lectura continua diferida para asegurar que el message loop esté activo
            arranqueLectorTimer = new System.Windows.Forms.Timer();
            arranqueLectorTimer.Interval = 200;
            arranqueLectorTimer.Tick += (s, e) =>
            {
                arranqueLectorTimer.Stop();
                arranqueLectorTimer.Dispose();
                arranqueLectorTimer = null;

                System.Diagnostics.Debug.WriteLine("[LOG MainTray] Message loop activo. Iniciando lectura continua del sensor...");
                biometricService.IniciarLectura();
            };
            arranqueLectorTimer.Start();

            // 4. Lanzar sincronización inicial y encender timer
            SincronizarConServidorAsync();
            syncTimer.Start();

            // 5. Inicializar el heartbeat periódico
            InicializarHeartbeat();
        }

        private void SincronizarConServidorAsync()
        {
            System.Diagnostics.Debug.WriteLine($"\n[LOG Sync] --- Inicio de ciclo de sincronización ({DateTime.Now:HH:mm:ss}) ---");

            ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    System.Diagnostics.Debug.WriteLine($"[LOG Sync] Solicitando empleados y huellas al servidor...");

                    List<Empleado> listaEmpleados = _apiClient.ObtenerEmpleados();
                    List<Huella> listaHuellas = _apiClient.ObtenerHuellas();

                    if (listaEmpleados != null && listaHuellas != null)
                    {
                        System.Diagnostics.Debug.WriteLine($"[LOG Sync] Datos recibidos de la API -> Empleados: {listaEmpleados.Count}, Huellas: {listaHuellas.Count}");

                        fichadaRepository.SincronizarCatalogoEmpresa(listaEmpleados, listaHuellas);

                        // Actualizar la caché local manteniendo la concurrencia segura
                        CargarHuellasLocales();
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("[LOG Sync] ADVERTENCIA: Falló la obtención de huellas/empleados. Se conservan los datos locales previos.");
                    }

                    // Envío de fichadas locales no sincronizadas
                    var pendientes = fichadaRepository.ObtenerFichadasPendientes();
                    var listaPendientes = new List<Fichada>(pendientes);
                    System.Diagnostics.Debug.WriteLine($"[LOG Sync] Fichadas pendientes de envío: {listaPendientes.Count}");

                    if (listaPendientes.Count > 0)
                    {
                        bool enviadas = _apiClient.EnviarFichadas(listaPendientes);
                        if (enviadas)
                        {
                            var ids = listaPendientes.ConvertAll(f => f.Id);
                            fichadaRepository.MarcarComoSincronizadas(ids);
                            System.Diagnostics.Debug.WriteLine($"[LOG Sync] {listaPendientes.Count} fichada(s) marcadas como sincronizadas en DB local.");
                            MostrarNotificacion("Sincronización Exitosa", $"{listaPendientes.Count} fichada(s) enviadas al servidor.", ToolTipIcon.Info);

                            bool limpiezaOk = fichadaRepository.LimpiarFichadasSincronizadas();
                            if (limpiezaOk)
                            {
                                System.Diagnostics.Debug.WriteLine("[LOG Sync] Limpieza de fichadas locales antiguas completada correctamente.");
                            }
                            else
                            {
                                System.Diagnostics.Debug.WriteLine("[LOG Sync] ADVERTENCIA: La sincronización en servidor fue exitosa, pero falló la limpieza local.");
                            }
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine("[LOG Sync] ERROR: La API rechazó el envío de fichadas pendientes.");
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[LOG Sync ERROR] Excepción durante la sincronización: {ex.Message}");
                }
            });
        }

        private void CargarHuellasLocales()
        {
            var locales = fichadaRepository.ObtenerHuellasLocales();
            huellasCargadas = new List<Huella>(locales);

            System.Diagnostics.Debug.WriteLine($"\n[LOG Biometric] Cache local actualizada: {huellasCargadas.Count} huella(s) en memoria.");

            int index = 1;
            foreach (var h in huellasCargadas)
            {
                int len = string.IsNullOrEmpty(h.TemplateBiometrico) ? 0 : h.TemplateBiometrico.Length;
                System.Diagnostics.Debug.WriteLine($"    -> Huella #{index}: EmpleadoId={h.EmpleadoId}, IndiceDedo={h.IndiceDedo}, LongitudTemplate={len} chars");
                index++;
            }
        }

        private void OnHuellaCapturada(Fmd fmdCapturado)
        {
            System.Diagnostics.Debug.WriteLine($"\n[LOG Biometric] --- Nueva huella apoyada en el lector ---");

            if (fmdCapturado == null)
            {
                System.Diagnostics.Debug.WriteLine("[LOG Biometric ERROR] El FMD recibido desde el SDK es NULO.");
                return;
            }

            int bytesLength = fmdCapturado.Bytes != null ? fmdCapturado.Bytes.Length : 0;
            System.Diagnostics.Debug.WriteLine($"[LOG Biometric] Captura exitosa -> Formato: {fmdCapturado.Format}, Tamaño Bytes: {bytesLength}");

            if (huellasCargadas == null || huellasCargadas.Count == 0)
            {
                System.Diagnostics.Debug.WriteLine("[LOG Biometric WARNING] No hay huellas cargadas en la memoria local para comparar.");
                OnHuellaNoReconocida();
                return;
            }

            System.Diagnostics.Debug.WriteLine($"[LOG Biometric] Iniciando comparación iterativa contra {huellasCargadas.Count} huellas almacenadas...");

            try
            {
                biometricService.IdentificarEmpleado(fmdCapturado, huellasCargadas);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LOG Biometric EXCEPCIÓN] Error en proceso de identificación: {ex.Message}");
            }
        }

        private void OnEmpleadoIdentificado(int empleadoId)
        {
            System.Diagnostics.Debug.WriteLine($"[LOG Biometric SUCCESS] ¡Match encontrado! Empleado ID identificado: {empleadoId}");

            var ultimaFichadaUsuario = fichadaRepository.ObtenerUltimaFichada(empleadoId);
            if (ultimaFichadaUsuario != null)
            {
                double minutosTranscurridos = (DateTime.Now - ultimaFichadaUsuario.FechaHora).TotalMinutes;
                if (minutosTranscurridos < 5)
                {
                    System.Diagnostics.Debug.WriteLine($"[LOG Biometric] Fichada ignorada. Pasaron solo {minutosTranscurridos:N1} minutos desde la última.");
                    System.Media.SystemSounds.Exclamation.Play();
                    System.Media.SystemSounds.Exclamation.Play();
                    MostrarNotificacion("Fichada Duplicada", "Ya registró su asistencia hace pocos minutos.", ToolTipIcon.Warning);
                    return;
                }
            }

            string tipoRegistro = DeterminarTipoRegistro(empleadoId, ultimaFichadaUsuario);

            var nuevaFichada = new Fichada
            {
                EmpleadoId = empleadoId,
                FechaHora = DateTime.Now,
                TipoRegistro = tipoRegistro,
                Metodo = "Biometrico",
                Sincronizado = false
            };

            fichadaRepository.GuardarFichadaLocal(nuevaFichada);
            System.Diagnostics.Debug.WriteLine($"[LOG Biometric] Fichada guardada en DB local para EmpleadoId: {empleadoId} como {tipoRegistro}");

            // Buscamos el DNI del empleado en la base de datos local usando su EmpleadoId
            string dniEmpleado = fichadaRepository.ObtenerDniPorEmpleadoId(empleadoId) ?? empleadoId.ToString();

            System.Media.SystemSounds.Asterisk.Play();
            MostrarNotificacion("Fichada Registrada", $"DNI: {dniEmpleado} - {tipoRegistro} a las {nuevaFichada.FechaHora:HH:mm:ss}", ToolTipIcon.Info);
        }

        private string DeterminarTipoRegistro(int empleadoId, Fichada ultimaFichada)
        {
            var horario = fichadaRepository.ObtenerHorarioLaboral(empleadoId);
            DateTime ahora = DateTime.Now;
            TimeSpan horaActual = ahora.TimeOfDay;

            if (horario != null)
            {
                int horaEntradaH = horario.HoraEntrada.Hours;

                bool esTrasdia = horaEntradaH >= 18 && horaEntradaH < 24;
                bool esNocturno = horaEntradaH >= 0 && horaEntradaH < 6;
                bool esDiurno = !esTrasdia && !esNocturno;

                if (ultimaFichada != null)
                {
                    DateTime fechaUltima = ultimaFichada.FechaHora;

                    if (esTrasdia)
                    {
                        TimeSpan diferenciaTiempo = ahora - fechaUltima;
                        if (ultimaFichada.TipoRegistro == "Entrada" && diferenciaTiempo.TotalHours < 16)
                        {
                            return "Salida";
                        }
                    }
                    else
                    {
                        if (fechaUltima.Date < ahora.Date)
                        {
                            return "Entrada";
                        }
                    }
                }
            }

            if (ultimaFichada != null)
            {
                return ultimaFichada.TipoRegistro == "Entrada" ? "Salida" : "Entrada";
            }

            return "Entrada";
        }

        private void OnHuellaNoReconocida()
        {
            System.Diagnostics.Debug.WriteLine("[LOG Biometric FAIL] No se encontró coincidencia (Match Score insuficiente) con ninguna huella en base.");
            MostrarNotificacion("Acceso Denegado", "Huella no reconocida o no registrada.", ToolTipIcon.Warning);
        }

        private void OnEstadoCambiado(string mensaje)
        {
            System.Diagnostics.Debug.WriteLine($"[LOG Biometric Status]: {mensaje}");
            string textoCompleto = $"DevsFingerPrint: {mensaje}";

            if (notifyIcon != null)
            {
                notifyIcon.Text = textoCompleto.Length > 63 ? textoCompleto.Substring(0, 60) + "..." : textoCompleto;

                if (mensaje.Contains("Lector listo") || mensaje.Contains("listo"))
                {
                    MostrarNotificacion("Sensor Activo", "El lector U.are.U está listo. Ya se puede fichar.", ToolTipIcon.Info);
                }
            }
        }

        private void MostrarNotificacion(string titulo, string mensaje, ToolTipIcon icono)
        {
            notifyIcon.ShowBalloonTip(3000, titulo, mensaje, icono);
        }

        private void MostrarEstadoLector(object sender, EventArgs e)
        {
            int cantidad = huellasCargadas != null ? huellasCargadas.Count : 0;

            MessageBox.Show($"Huellas en caché: {cantidad}\nBase de datos local: Listo\nSerial del lector: {biometricService.ObtenerLectorActual().Description.SerialNumber}", "DevsFingerPrint - Estado", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void EnrolarNuevoEmpleado(object sender, EventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("[LOG Enrolar] Pausando servicio biométrico continuo...");
            biometricService.DetenerLectura();

            try
            {
                ReaderCollection readers = ReaderCollection.GetReaders();
                Reader lectorFisico = (readers != null && readers.Count > 0) ? readers[0] : null;

                if (lectorFisico == null)
                {
                    System.Diagnostics.Debug.WriteLine("[LOG Enrolar ERROR] No se detectó ningún hardware lector conectado.");
                    MessageBox.Show("No hay ningún lector U.are.U 4500 conectado.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                System.Diagnostics.Debug.WriteLine("[LOG Enrolar] Obteniendo lista de empleados desde la API...");
                List<Empleado> listaEmpleados = _apiClient.ObtenerEmpleados();

                // Obtenemos las huellas locales actuales desde el repositorio
                var huellasLocales = fichadaRepository.ObtenerHuellasLocales();

                // Pasamos la lista de empleados y la colección completa de huellas locales al formulario
                using (var frmEnrolar = new EnrolarHuellaForm(lectorFisico, listaEmpleados, huellasLocales))
                {
                    if (frmEnrolar.ShowDialog() == DialogResult.OK && frmEnrolar.HuellaCapturada != null)
                    {
                        Huella nuevaHuella = frmEnrolar.HuellaCapturada;
                        int indiceDedo = frmEnrolar.IndiceDedoSeleccionado;

                        System.Diagnostics.Debug.WriteLine($"[LOG Enrolar] Huella capturada exitosamente. EmpleadoId: {nuevaHuella.EmpleadoId}, Dedo: {indiceDedo}");

                        ThreadPool.QueueUserWorkItem(_ =>
                        {
                            System.Diagnostics.Debug.WriteLine("[LOG Enrolar API] Enviando nueva huella a la API...");
                            bool subida = _apiClient.GuardarHuella(nuevaHuella, indiceDedo);

                            if (subida)
                            {
                                System.Diagnostics.Debug.WriteLine("[LOG Enrolar API] Huella enviada con éxito.");
                                MostrarNotificacion("Enrolamiento", "La huella fue subida correctamente al servidor central.", ToolTipIcon.Info);
                            }
                            else
                            {
                                System.Diagnostics.Debug.WriteLine("[LOG Enrolar API ERROR] La API devolvió un fallo al intentar guardar la huella.");
                            }
                        });

                        CargarHuellasLocales();
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("[LOG Enrolar] Enrolamiento cancelado por el usuario.");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LOG Enrolar EXCEPCIÓN]: {ex.Message}");
                MessageBox.Show($"Ocurrió un error inesperado en el enrolamiento: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                System.Diagnostics.Debug.WriteLine("[LOG Enrolar] Reanudando lectura continua de fichadas...");
                biometricService.IniciarLectura();
            }
        }

        private void SalirAplicacion(object sender, EventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("[LOG MainTray] Cerrando aplicación.");
            biometricService.DetenerLectura();
            DetenerHeartbeat();

            if (lectorFormularioOculto != null && !lectorFormularioOculto.IsDisposed)
            {
                lectorFormularioOculto.Dispose();
            }

            notifyIcon.Visible = false;
            Application.Exit();
        }

        private void InicializarHeartbeat()
        {
            System.Diagnostics.Debug.WriteLine("[LOG TRAY] Configurando temporizador de Heartbeat...");

            // Instanciamos el timer (ej. cada 5 minutos: 60000 ms * 5)
            heartbeatTimer = new System.Windows.Forms.Timer();
            heartbeatTimer.Interval = 30000; // 5 minutos (podes ajustarlo si querés probar más rápido)
            heartbeatTimer.Tick += (sender, e) => EjecutarHeartbeatPeriodico();
            heartbeatTimer.Start();

            System.Diagnostics.Debug.WriteLine("[LOG TRAY] Temporizador de Heartbeat iniciado correctamente.");

            // Opcional: Lanzar un latido inmediatamente al arrancar la app en segundo plano
            EjecutarHeartbeatPeriodico();
        }

        private void EjecutarHeartbeatPeriodico()
        {
            System.Diagnostics.Debug.WriteLine("[LOG TRAY] Ejecutando hilo en segundo plano para enviar heartbeat...");

            // Usamos ThreadPool para evitar bloquear la interfaz gráfica o el lector de huellas
            System.Threading.ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    // Asumiendo que tenés acceso a tu instancia de ApiClient (ej: _apiClient)
                    bool resultado = _apiClient.EnviarHeartbeat();

                    if (resultado)
                    {
                        System.Diagnostics.Debug.WriteLine("[LOG TRAY] Heartbeat periódico completado y confirmado con éxito.");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("[LOG TRAY] ADVERTENCIA: El servidor no pudo procesar o confirmar el heartbeat.");
                    }
                }
                catch (Exception ex)
                {
                    // Silencioso para no interrumpir al usuario si hay problemas de conectividad intermitentes
                    System.Diagnostics.Debug.WriteLine($"[LOG TRAY ERROR] Excepción no controlada en el hilo de heartbeat: {ex.Message}");
                }
            });
        }

        // Recordá llamar a este método para limpiar el timer cuando se cierre la aplicación (ej: en SalirAplicacion)
        private void DetenerHeartbeat()
        {
            if (heartbeatTimer != null)
            {
                System.Diagnostics.Debug.WriteLine("[LOG TRAY] Deteniendo y liberando recursos del temporizador de Heartbeat...");
                heartbeatTimer.Stop();
                heartbeatTimer.Dispose();
                heartbeatTimer = null;
            }
        }
    }
}