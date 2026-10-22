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
        private int sincronizacionEnCurso;

        private ContextMenuStrip contextMenu;
#if DEBUG
        private ToolStripMenuItem menuSimularFichada;
#endif

        public MainTrayContext(ApiClient apiClient)
        {
            System.Diagnostics.Debug.WriteLine("[LOG MainTray] Inicializando MainTrayContext...");
            LocalDatabase.Inicializar();

            _apiClient = apiClient;
            lectorFormularioOculto = new LectorListenerForm();
            lectorFormularioOculto.CreateControl();
            fichadaRepository = new FichadaRepository();
#if DEBUG
            biometricService = new BiometricService(_apiClient);
#else
            biometricService = new DigitalPersonaBiometricService();
#endif

            huellasCargadas = new List<Huella>();

            contextMenu = new ContextMenuStrip();

            contextMenu.Items.Add(
#if DEBUG
                biometricService is BiometricService ? "Enrolar nueva huella (Mock)" : "Enrolar nueva huella",
#else
                "Enrolar nueva huella",
#endif
                null,
                EnrolarNuevoEmpleado);

#if DEBUG
            contextMenu.Items.Add("-");

            // Submenú para simular fichadas por empleado específico desde el tray
            menuSimularFichada = new ToolStripMenuItem("Simular Fichada por Empleado");
            contextMenu.Items.Add(menuSimularFichada);

            contextMenu.Items.Add("-");
            contextMenu.Items.Add("Estado del Lector (Mock)", null, MostrarEstadoLector);
#else
            contextMenu.Items.Add("Estado del lector biométrico", null, MostrarEstadoLector);
#endif
            contextMenu.Items.Add("-");
            contextMenu.Items.Add("Salir", null, SalirAplicacion);

            notifyIcon = new NotifyIcon
            {
                Icon = Properties.Resources.circularColor,
                ContextMenuStrip = contextMenu,
                Visible = true,
#if DEBUG
                Text = "DevsFingerPrint - Control de Fichajes (Modo Mock)"
#else
                Text = "DevsFingerPrint - Lector DigitalPersona"
#endif
            };

            // Eventos del servicio biométrico
            biometricService.OnHuellaCapturada += OnHuellaCapturada;
            biometricService.OnEmpleadoIdentificado += OnEmpleadoIdentificado;
            biometricService.OnHuellaNoReconocida += OnHuellaNoReconocida;
            biometricService.OnEstadoCambiado += OnEstadoCambiado;

            // Temporizador de sincronización periódica
            syncTimer = new System.Windows.Forms.Timer();
            syncTimer.Interval = 60000 * 20;
            syncTimer.Tick += (s, e) => SincronizarConServidorAsync();

            CargarHuellasLocales();

#if DEBUG
            // Construir el submenú dinámico de empleados para simular fichadas
            ConstruirMenuEmpleadosSimulacion();
#endif

            arranqueLectorTimer = new System.Windows.Forms.Timer();
            arranqueLectorTimer.Interval = 200;
            arranqueLectorTimer.Tick += (s, e) =>
            {
                arranqueLectorTimer.Stop();
                arranqueLectorTimer.Dispose();
                arranqueLectorTimer = null;

                if (!biometricService.IniciarLectura())
                {
                    MostrarNotificacion(
                        "Lector biométrico no disponible",
                        "Conecte el lector U.are.U 4500 para habilitar el fichaje.",
                        ToolTipIcon.Error);
                }
            };
            arranqueLectorTimer.Start();

            SincronizarConServidorAsync();
            syncTimer.Start();
            InicializarHeartbeat();
        }

#if DEBUG
        private void ConstruirMenuEmpleadosSimulacion()
        {
            menuSimularFichada.DropDownItems.Clear();
            try
            {
                List<Empleado> listaEmpleados = _apiClient.ObtenerEmpleados();
                if (listaEmpleados != null && listaEmpleados.Count > 0)
                {
                    foreach (var emp in listaEmpleados)
                    {
                        string textoItem = $"{emp.NombreCompleto} (ID: {emp.Id})";
                        menuSimularFichada.DropDownItems.Add(textoItem, null, (s, e) =>
                        {
                            System.Diagnostics.Debug.WriteLine($"[LOG Mock Tray] Simulando fichada directa para: {emp.NombreCompleto} (ID: {emp.Id})");

                            // Disparamos directamente la identificación del empleado seleccionado
                            OnEmpleadoIdentificado(emp.Id);
                        });
                    }
                }
                else
                {
                    menuSimularFichada.DropDownItems.Add("(No hay empleados disponibles)").Enabled = false;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LOG Mock Tray ERROR] No se pudieron cargar empleados para el submenú: {ex.Message}");
                menuSimularFichada.DropDownItems.Add("(Error al cargar empleados)").Enabled = false;
            }
        }
#endif

        private void SincronizarConServidorAsync()
        {
            ThreadPool.QueueUserWorkItem(_ => EjecutarSincronizacionServidor());
        }

        private void EjecutarSincronizacionServidor()
        {
            if (Interlocked.Exchange(ref sincronizacionEnCurso, 1) != 0)
            {
                return;
            }

            System.Diagnostics.Debug.WriteLine($"\n[LOG Sync] --- Inicio de ciclo de sincronización ({DateTime.Now:HH:mm:ss}) ---");

            try
            {
                List<Empleado> listaEmpleados = _apiClient.ObtenerEmpleados();
                List<Huella> listaHuellas = _apiClient.ObtenerHuellas();

                if (listaEmpleados != null && listaHuellas != null)
                {
                    fichadaRepository.SincronizarCatalogoEmpresa(listaEmpleados, listaHuellas);
                    CargarHuellasLocales();

                    // Refresca el menú de simulación solo en compilaciones de depuración.
#if DEBUG
                    if (menuSimularFichada.Owner?.InvokeRequired == true)
                    {
                        menuSimularFichada.Owner.Invoke(new Action(ConstruirMenuEmpleadosSimulacion));
                    }
                    else
                    {
                        ConstruirMenuEmpleadosSimulacion();
                    }
#endif
                }

                var pendientes = fichadaRepository.ObtenerFichadasPendientes();
                var listaPendientes = new List<Fichada>(pendientes);
                if (listaPendientes.Count > 500)
                {
                    listaPendientes = listaPendientes.GetRange(0, 500);
                }

                if (listaPendientes.Count > 0)
                {
                    bool enviadas = _apiClient.EnviarFichadas(listaPendientes);
                    if (enviadas)
                    {
                        var ids = listaPendientes.ConvertAll(f => f.Id);
                        fichadaRepository.MarcarComoSincronizadas(ids);
                        MostrarNotificacion("Sincronización Exitosa", $"{listaPendientes.Count} fichada(s) enviadas al servidor.", ToolTipIcon.Info);
                        fichadaRepository.LimpiarFichadasSincronizadas();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LOG Sync ERROR] Excepción durante la sincronización: {ex.Message}");
            }
            finally
            {
                Interlocked.Exchange(ref sincronizacionEnCurso, 0);
            }
        }

        private void CargarHuellasLocales()
        {
            var locales = fichadaRepository.ObtenerHuellasLocales();
            huellasCargadas = new List<Huella>(locales);
#if DEBUG
            System.Diagnostics.Debug.WriteLine($"\n[LOG Biometric MOCK] Cache local actualizada: {huellasCargadas.Count} huella(s) en memoria.");
#else
            System.Diagnostics.Debug.WriteLine($"\n[LOG Biometric] Cache local actualizada: {huellasCargadas.Count} huella(s) en memoria.");
#endif
        }

        private void OnHuellaCapturada(Fmd fmdCapturado)
        {
            System.Diagnostics.Debug.WriteLine("\n[LOG Biometric] Huella capturada.");
            if (huellasCargadas == null || huellasCargadas.Count == 0)
            {
                OnHuellaNoReconocida();
                return;
            }

            try
            {
                biometricService.IdentificarEmpleado(fmdCapturado, huellasCargadas);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LOG Biometric EXCEPCIÓN]: {ex.Message}");
            }
        }

        private void OnEmpleadoIdentificado(int empleadoId)
        {
            System.Diagnostics.Debug.WriteLine($"[LOG Biometric SUCCESS] ¡Match encontrado! Empleado ID identificado: {empleadoId}");

            var ultimaFichadaUsuario = fichadaRepository.ObtenerUltimaFichada(empleadoId);
            DateTime ahora = DateTime.Now;
            if (FichadaRules.EsDuplicada(ultimaFichadaUsuario, ahora))
            {
                double minutosTranscurridos = (ahora - ultimaFichadaUsuario.FechaHora).TotalMinutes;
                System.Diagnostics.Debug.WriteLine($"[LOG Biometric] Fichada ignorada por duplicidad reciente ({minutosTranscurridos:N1} min).");
                System.Media.SystemSounds.Exclamation.Play();
                MostrarNotificacion("Fichada Duplicada", "Ya registró su asistencia hace pocos minutos.", ToolTipIcon.Warning);
                return;
            }

            string tipoRegistro = FichadaRules.DeterminarTipoRegistro(ultimaFichadaUsuario, ahora);

            var nuevaFichada = new Fichada
            {
                EmpleadoId = empleadoId,
                FechaHora = ahora,
                TipoRegistro = tipoRegistro,
                Metodo = "Biometrico",
                Sincronizado = false
            };

            fichadaRepository.GuardarFichadaLocal(nuevaFichada);
#if DEBUG
            string dniEmpleado = fichadaRepository.ObtenerDniPorEmpleadoId(empleadoId) ?? empleadoId.ToString();
#endif

            System.Media.SystemSounds.Asterisk.Play();
#if DEBUG
            MostrarNotificacion("Fichada Registrada (Mock)", $"DNI: {dniEmpleado} - {tipoRegistro} a las {nuevaFichada.FechaHora:HH:mm:ss}", ToolTipIcon.Info);
#else
            MostrarNotificacion("Fichada Registrada", $"{tipoRegistro} a las {nuevaFichada.FechaHora:HH:mm:ss}", ToolTipIcon.Info);
#endif
        }

        private void OnHuellaNoReconocida()
        {
            System.Diagnostics.Debug.WriteLine("[LOG Biometric FAIL] Huella no reconocida.");
            MostrarNotificacion("Acceso Denegado", "Huella no reconocida.", ToolTipIcon.Warning);
        }

        private void OnEstadoCambiado(string mensaje)
        {
            System.Diagnostics.Debug.WriteLine($"[LOG Biometric Status]: {mensaje}");
            if (notifyIcon != null)
            {
#if DEBUG
                string texto = $"DevsFingerPrint (Mock): {mensaje}";
#else
                string texto = $"DevsFingerPrint: {mensaje}";
#endif
                if (!string.IsNullOrEmpty(texto) && texto.Length > 63)
                {
                    texto = texto.Substring(0, 63);
                }

                notifyIcon.Text = texto;
            }
        }

        private void MostrarNotificacion(string titulo, string mensaje, ToolTipIcon icono)
        {
            notifyIcon.ShowBalloonTip(3000, titulo, mensaje, icono);
        }

        private void MostrarEstadoLector(object sender, EventArgs e)
        {
#if DEBUG
            int cantidad = huellasCargadas != null ? huellasCargadas.Count : 0;
            MessageBox.Show($"[MODO MOCK] Huellas en caché: {cantidad}\nSimulador de Lector Activo", "DevsFingerPrint - Estado", MessageBoxButtons.OK, MessageBoxIcon.Information);
#else
            var lector = biometricService.ObtenerLectorActual();
            if (lector == null)
            {
                MessageBox.Show(
                    "No hay un lector DigitalPersona conectado. Conecte el U.are.U 4500 para enrolar o fichar.",
                    "DevsFingerPrint - Estado",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            MessageBox.Show(
                $"Lector: {lector.Description.Name}\nNúmero de serie: {lector.Description.SerialNumber}",
                "DevsFingerPrint - Estado",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
#endif
        }

        private void EnrolarNuevoEmpleado(object sender, EventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("[LOG Enrolar] Abriendo formulario de enrolamiento.");
            biometricService.DetenerLectura();

            try
            {
                List<Empleado> listaEmpleados = _apiClient.ObtenerEmpleados();
                var huellasLocales = fichadaRepository.ObtenerHuellasLocales();

                using (var frmEnrolar = new EnrolarHuellaForm(biometricService, listaEmpleados, huellasLocales))
                {
                    if (frmEnrolar.ShowDialog() == DialogResult.OK && frmEnrolar.HuellaCapturada != null)
                    {
                        Huella nuevaHuella = frmEnrolar.HuellaCapturada;
                        int indiceDedo = frmEnrolar.IndiceDedoSeleccionado;

                        System.Diagnostics.Debug.WriteLine($"[LOG Enrolar] Enviando template para empleado ID {nuevaHuella.EmpleadoId}.");

                        bool subida = _apiClient.GuardarHuella(nuevaHuella, indiceDedo);

                        if (subida)
                        {
                            MostrarNotificacion("Enrolamiento exitoso", "La huella fue guardada correctamente.", ToolTipIcon.Info);
                            CargarHuellasLocales();
                            EjecutarSincronizacionServidor();
                        }
                        else
                        {
                            MessageBox.Show("Error al intentar guardar la huella en el servidor.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LOG Enrolar EXCEPCIÓN]: {ex.Message}");
                MessageBox.Show($"Ocurrió un error: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                biometricService.IniciarLectura();
            }
        }

        private void SalirAplicacion(object sender, EventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("[LOG MainTray] Cerrando aplicación.");
            biometricService.DetenerLectura();
            (biometricService as IDisposable)?.Dispose();
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
            heartbeatTimer = new System.Windows.Forms.Timer();
            heartbeatTimer.Interval = 30000;
            heartbeatTimer.Tick += (sender, e) => EjecutarHeartbeatPeriodico();
            heartbeatTimer.Start();
            EjecutarHeartbeatPeriodico();
        }

        private void EjecutarHeartbeatPeriodico()
        {
            ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    _apiClient.EnviarHeartbeat();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[LOG TRAY ERROR] Excepción en heartbeat: {ex.Message}");
                }
            });
        }

        private void DetenerHeartbeat()
        {
            if (heartbeatTimer != null)
            {
                heartbeatTimer.Stop();
                heartbeatTimer.Dispose();
                heartbeatTimer = null;
            }
        }
    }
}