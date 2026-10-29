using DevsFingerPrint.Domain.Interfaces;
using DevsFingerPrint.Domain.Models;
using DevsFingerPrint.Infrastructure.Data;
using DevsFingerPrint.Infrastructure.Repositories;
using DevsFingerPrint.Infrastructure.Services;
using DPUruNet;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;

namespace DevsFingerPrint.Presentation
{
    public class MainTrayContext : ApplicationContext
    {
        private readonly NotifyIcon notifyIcon;
        private readonly GestorIconoBandeja gestorIcono = new GestorIconoBandeja();
        private readonly ReintentoManual reintentoManual = new ReintentoManual();
        private readonly IBiometricService biometricService;
        private readonly IFichadaRepository fichadaRepository;
        private List<HuellaEnMemoria> huellasCargadas;
        private ResumenCatalogoBiometrico resumenCatalogo = new ResumenCatalogoBiometrico();
        private string estadoApi = "Sin sincronizar";
        private ApiClient _apiClient;
        private readonly LectorListenerForm lectorFormularioOculto;
        private System.Windows.Forms.Timer heartbeatTimer;
        private readonly System.Windows.Forms.Timer syncTimer;
        private readonly System.Windows.Forms.Timer fichadasTimer;
        private System.Windows.Forms.Timer arranqueLectorTimer;
        private readonly OperacionExclusiva sincronizacionExclusiva = new OperacionExclusiva();
        private readonly OperacionExclusiva heartbeatExclusivo = new OperacionExclusiva();
        private readonly PoliticaIntervalo politicaHeartbeat = new PoliticaIntervalo(CicloVidaAgente.IntervaloHeartbeatNormalMs, false);
        private readonly PoliticaIntervalo politicaCatalogo = new PoliticaIntervalo(CicloVidaAgente.IntervaloSincronizacionNormalMs, true);
        private readonly PoliticaIntervalo politicaFichadas = new PoliticaIntervalo(CicloVidaAgente.IntervaloRecuperacionFichadasNormalMs, false);
        private EnvioFichadasPendientes envioPendientes;
        private bool lectorDisponible;
        private bool sincronizando;
        private bool apiConectada;
        private int recursosLiberados;
        private bool notificoCredenciales;
        private MotivoCierre? motivoCierre;

        private ContextMenuStrip contextMenu;
        private readonly ToolStripMenuItem menuEstado;
        private readonly ToolStripMenuItem menuSincronizacion;
#if DEBUG
        private ToolStripMenuItem menuSimularFichada;
#endif

        public MainTrayContext(ApiClient apiClient, InstanciaUnicaAgente instancia)
        {
            System.Diagnostics.Debug.WriteLine("[LOG MainTray] Inicializando MainTrayContext...");
            LocalDatabase.Inicializar();

            _apiClient = apiClient;
            lectorFormularioOculto = new LectorListenerForm();
            lectorFormularioOculto.CreateControl();
            fichadaRepository = new FichadaRepository();
            envioPendientes = new EnvioFichadasPendientes(
                () => fichadaRepository.ObtenerFichadasPendientes(),
                lote => _apiClient.ClasificarEnvioFichadas(lote),
                ids => fichadaRepository.MarcarComoSincronizadas(ids));
#if DEBUG
            biometricService = new BiometricService(_apiClient);
#else
            biometricService = new DigitalPersonaBiometricService();
#endif

            huellasCargadas = new List<HuellaEnMemoria>();

            contextMenu = new ContextMenuStrip();
            menuEstado = new ToolStripMenuItem(CicloVidaAgente.TextoEstado(EstadoOperativo.SinConexion));
            menuEstado.Enabled = false;
            menuSincronizacion = new ToolStripMenuItem(CicloVidaAgente.TextoUltimaSincronizacion(null));
            menuSincronizacion.Enabled = false;
            ToolStripMenuItem menuVersion = new ToolStripMenuItem(
                CicloVidaAgente.TextoVersion(Assembly.GetExecutingAssembly().GetName().Version));
            menuVersion.Enabled = false;
            contextMenu.Items.Add(menuEstado);
            contextMenu.Items.Add(menuSincronizacion);
            contextMenu.Items.Add("Reintentar conexión", null, ReintentarConexion);
            contextMenu.Items.Add("Vincular o cambiar credenciales", null, VincularCredenciales);
            contextMenu.Items.Add("Abrir carpeta de logs", null, AbrirCarpetaLogs);
            contextMenu.Items.Add(menuVersion);
            contextMenu.Items.Add("-");

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

            gestorIcono.CrearIcono();
            notifyIcon = new NotifyIcon
            {
                Icon = Properties.Resources.circularColor,
                ContextMenuStrip = contextMenu,
                Visible = true,
                Text = CicloVidaAgente.TextoEstado(EstadoOperativo.SinConexion)
            };

            // Eventos del servicio biométrico
            biometricService.OnHuellaCapturada += OnHuellaCapturada;
            biometricService.OnEmpleadoIdentificado += OnEmpleadoIdentificado;
            biometricService.OnHuellaNoReconocida += OnHuellaNoReconocida;
            biometricService.OnEstadoCambiado += OnEstadoCambiado;
            if (gestorIcono.SuscribirBarra())
            {
                lectorFormularioOculto.BarraTareasCreada += OnBarraTareasCreada;
            }
            SystemEvents.SessionEnding += OnSessionEnding;

            // Temporizador de sincronización periódica
            gestorIcono.RegistrarTimer();
            syncTimer = new System.Windows.Forms.Timer();
            syncTimer.Interval = politicaCatalogo.IntervaloMs;
            syncTimer.Tick += (s, e) => SincronizarConServidorAsync();
            gestorIcono.RegistrarTimer();
            fichadasTimer = new System.Windows.Forms.Timer();
            fichadasTimer.Interval = politicaFichadas.IntervaloMs;
            fichadasTimer.Tick += (s, e) => EnviarFichadasPendientesAsync();

            CargarHuellasLocales();

#if DEBUG
            // Construir el submenú dinámico de empleados para simular fichadas
            ConstruirMenuEmpleadosSimulacion();
#endif

            gestorIcono.RegistrarTimer();
            arranqueLectorTimer = new System.Windows.Forms.Timer();
            arranqueLectorTimer.Interval = 200;
            arranqueLectorTimer.Tick += (s, e) =>
            {
                arranqueLectorTimer.Stop();
                arranqueLectorTimer.Dispose();
                arranqueLectorTimer = null;

                lectorDisponible = biometricService.IniciarLectura();
                PublicarIcono();
                if (!lectorDisponible)
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
            EnviarFichadasPendientesAsync();
            fichadasTimer.Start();
            InicializarHeartbeat();
            if (instancia != null)
            {
                instancia.Escuchar(MostrarSenalDeOtraInstancia);
            }
        }

#if DEBUG
        private void ConstruirMenuEmpleadosSimulacion()
        {
            menuSimularFichada.DropDownItems.Clear();
            try
            {
                ResultadoLista<Empleado> listaEmpleados = _apiClient.ObtenerEmpleados();
                if (listaEmpleados.Exitoso && listaEmpleados.Elementos.Count > 0)
                {
                    foreach (var emp in listaEmpleados.Elementos)
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
            ColaTrabajoAgente.Encolar(EjecutarSincronizacionServidor);
        }

        private void EjecutarSincronizacionServidor()
        {
            if (Volatile.Read(ref recursosLiberados) != 0)
            {
                return;
            }

            if (!sincronizacionExclusiva.TryEnter())
            {
                return;
            }

            sincronizando = true;
            PublicarIconoEnUi();
            AgentLog.Info("Inicio de sincronizacion.");
            System.Diagnostics.Debug.WriteLine($"\n[LOG Sync] --- Inicio de ciclo de sincronización ({DateTime.Now:HH:mm:ss}) ---");

            try
            {
                ResultadoLista<Empleado> listaEmpleados = _apiClient.ObtenerEmpleados();
                ResultadoLista<Huella> listaHuellas = _apiClient.ObtenerHuellas();

                List<Empleado> empleadosDestino = null;
                List<Huella> huellasDestino = null;
                string motivoContrato = "sin-contrato";
                bool contratoValido = listaEmpleados.Exitoso
                    && listaHuellas.Exitoso
                    && CatalogoBiometrico.PrepararReemplazo(
                        listaEmpleados.Elementos,
                        listaHuellas.Elementos,
                        LocalDatabase.ObtenerSucursalIdLocal(),
                        out empleadosDestino,
                        out huellasDestino,
                        out motivoContrato);
                if (contratoValido)
                {
                    fichadaRepository.SincronizarCatalogoEmpresa(empleadosDestino, huellasDestino);
                    resumenCatalogo.UltimaSincronizacionCorrecta = DateTime.Now;
                    apiConectada = true;
                    politicaCatalogo.RegistrarRecuperacion();
                    AplicarIntervalosEnUi();
                    estadoApi = "Conectada";
                    CargarHuellasLocales();
                    AgentLog.Info("Sincronizacion biometrica correcta. api-empleados=" + listaEmpleados.Elementos.Count
                        + " api-huellas=" + listaHuellas.Elementos.Count
                        + " " + resumenCatalogo.TextoLog()
                        + " ultima=" + resumenCatalogo.UltimaSincronizacionCorrecta.Value.ToString("yyyy-MM-dd HH:mm:ss"));

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
                else
                {
                    apiConectada = listaEmpleados.Exitoso && listaHuellas.Exitoso;
                    if (!apiConectada && !_apiClient.CredencialesRechazadas && (CicloVidaAgente.EsFalloTemporal(listaEmpleados.Motivo) || CicloVidaAgente.EsFalloTemporal(listaHuellas.Motivo)))
                    {
                        politicaCatalogo.RegistrarFalloTemporal();
                        AplicarIntervalosEnUi();
                    }

                    estadoApi = _apiClient.CredencialesRechazadas
                        ? "Credenciales no válidas"
                        : (listaEmpleados.Exitoso && listaHuellas.Exitoso ? "Respuesta rechazada" : "Sin conexión");
                    string motivo = listaEmpleados.Exitoso && listaHuellas.Exitoso
                        ? motivoContrato
                        : "empleados=" + listaEmpleados.Motivo + " huellas=" + listaHuellas.Motivo;
                    AgentLog.Info("Sincronizacion biometrica conservada. " + motivo);
                }

                AgentLog.Info("Sincronizacion finalizada.");
            }
            catch (Exception ex)
            {
                apiConectada = false;
                politicaCatalogo.RegistrarFalloTemporal();
                AplicarIntervalosEnUi();
                estadoApi = "Sin conexión";
                AgentLog.Error("Sincronizacion con error: " + ex.GetType().Name + ".");
                System.Diagnostics.Debug.WriteLine($"[LOG Sync ERROR] Excepción durante la sincronización: {ex.Message}");
            }
            finally
            {
                sincronizando = false;
                sincronizacionExclusiva.Salir();
                PublicarIconoEnUi();
            }
        }

        private void CargarHuellasLocales()
        {
            DateTime? ultima = resumenCatalogo.UltimaSincronizacionCorrecta;
            resumenCatalogo = CatalogoBiometrico.Construir(
                fichadaRepository.ObtenerEmpleadosLocales(),
                fichadaRepository.ObtenerHuellasLocales());
            resumenCatalogo.UltimaSincronizacionCorrecta = ultima;
            huellasCargadas = resumenCatalogo.Compatibles;
        }

        private void OnHuellaCapturada(Fmd fmdCapturado)
        {
            System.Diagnostics.Debug.WriteLine("\n[LOG Biometric] Huella capturada.");
            if (huellasCargadas == null || huellasCargadas.Count == 0)
            {
                if (resumenCatalogo.HuellasRecibidas > 0)
                {
                    MostrarNotificacion("Lector", "No hay huellas compatibles disponibles para validar.", ToolTipIcon.Warning);
                    return;
                }

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
            EnviarFichadasPendientesAsync();
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
            lectorDisponible = InterpretarLectorDisponible(mensaje, lectorDisponible);
            PublicarIcono();
        }

        private static bool InterpretarLectorDisponible(string mensaje, bool estadoActual)
        {
            if (string.IsNullOrEmpty(mensaje))
            {
                return estadoActual;
            }

            string texto = mensaje.ToLowerInvariant();
            if (texto.Contains("no se detect") || texto.Contains("no está conectado") || texto.Contains("no esta conectado") || texto.Contains("error"))
            {
                return false;
            }

            if (texto.Contains("lector conectado") || texto.Contains("listo"))
            {
                return true;
            }

            return estadoActual;
        }

        private void MostrarNotificacion(string titulo, string mensaje, ToolTipIcon icono)
        {
            notifyIcon.ShowBalloonTip(3000, titulo, mensaje, icono);
        }

        private void MostrarEstadoLector(object sender, EventArgs e)
        {
            var lector = biometricService.ObtenerLectorActual();
            bool conectado = lector != null;
            string serial = conectado ? lector.Description.SerialNumber : null;
            int pendientes = 0;
            try
            {
                pendientes = new List<Fichada>(fichadaRepository.ObtenerFichadasPendientes()).Count;
            }
            catch (Exception)
            {
                pendientes = 0;
            }

            MessageBox.Show(
                CatalogoBiometrico.TextoEstado(conectado, serial, resumenCatalogo, pendientes, estadoApi),
                "Estado del lector",
                MessageBoxButtons.OK,
                conectado ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
        }

        private void EnrolarNuevoEmpleado(object sender, EventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("[LOG Enrolar] Abriendo formulario de enrolamiento.");
            biometricService.DetenerLectura();

            try
            {
                ResultadoLista<Empleado> empleadosRemotos = _apiClient.ObtenerEmpleados();
                if (!empleadosRemotos.Exitoso)
                {
                    MessageBox.Show("No se pudo obtener el listado de empleados.", "Enrolamiento", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                List<Empleado> listaEmpleados = empleadosRemotos.Elementos;
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

        private void VincularCredenciales(object sender, EventArgs e)
        {
            if (Volatile.Read(ref recursosLiberados) != 0)
            {
                return;
            }

            biometricService.DetenerLectura();
            try
            {
                using (var formulario = new FormLogin(_apiClient))
                {
                    bool cancelado = formulario.ShowDialog() != DialogResult.OK;
                    if (cancelado)
                    {
                        AgentLog.Info("Vinculacion cancelada. El agente permanece activo.");
                    }
                    else
                    {
                        notificoCredenciales = false;
                        apiConectada = true;
                        politicaHeartbeat.RegistrarRecuperacion();
                        AplicarIntervalosEnUi();
                        SincronizarConServidorAsync();
                        EnviarFichadasPendientesAsync();
                    }
                }
            }
            finally
            {
                lectorDisponible = biometricService.IniciarLectura();
                PublicarIcono();
            }
        }

        private void SalirAplicacion(object sender, EventArgs e)
        {
            motivoCierre = MotivoCierre.Usuario;
            AgentLog.Info(CicloVidaAgente.TextoCierre(MotivoCierre.Usuario));
            LiberarRecursos();
            Application.Exit();
        }

        protected override void ExitThreadCore()
        {
            if (motivoCierre == null)
            {
                motivoCierre = MotivoCierre.Contexto;
                AgentLog.Info(CicloVidaAgente.TextoCierre(MotivoCierre.Contexto));
            }

            LiberarRecursos();
            base.ExitThreadCore();
        }

        private void OnSessionEnding(object sender, SessionEndingEventArgs e)
        {
            motivoCierre = MotivoCierre.SesionWindows;
            AgentLog.Info(CicloVidaAgente.TextoCierre(MotivoCierre.SesionWindows));
            try
            {
                if (lectorFormularioOculto != null && lectorFormularioOculto.IsHandleCreated && !lectorFormularioOculto.IsDisposed)
                {
                    lectorFormularioOculto.BeginInvoke(new Action(LiberarRecursos));
                }
                else
                {
                    LiberarRecursos();
                }
            }
            catch (Exception ex)
            {
                AgentLog.Error("Cierre de sesion con error: " + ex.GetType().Name + ".");
            }
        }

        private void LiberarRecursos()
        {
            if (Interlocked.Exchange(ref recursosLiberados, 1) != 0)
            {
                return;
            }

            gestorIcono.Cerrar();

            try
            {
                SystemEvents.SessionEnding -= OnSessionEnding;
            }
            catch (Exception)
            {
            }

            if (lectorFormularioOculto != null)
            {
                lectorFormularioOculto.BarraTareasCreada -= OnBarraTareasCreada;
            }

            if (syncTimer != null)
            {
                syncTimer.Stop();
            }

            if (fichadasTimer != null)
            {
                fichadasTimer.Stop();
            }

            if (heartbeatTimer != null)
            {
                heartbeatTimer.Stop();
                heartbeatTimer.Dispose();
                heartbeatTimer = null;
            }

            try
            {
                biometricService.DetenerLectura();
                (biometricService as IDisposable)?.Dispose();
            }
            catch (Exception ex)
            {
                AgentLog.Error("Cierre del lector con error: " + ex.GetType().Name + ".");
            }

            try
            {
                SQLiteConnection.ClearAllPools();
            }
            catch (Exception)
            {
            }

            if (notifyIcon != null)
            {
                notifyIcon.Visible = false;
                notifyIcon.Dispose();
            }

            if (lectorFormularioOculto != null && !lectorFormularioOculto.IsDisposed)
            {
                lectorFormularioOculto.Dispose();
            }
        }

        private void EnviarFichadasPendientesAsync()
        {
            ColaTrabajoAgente.Encolar(EjecutarEnvioFichadasPendientes);
        }

        private void EjecutarEnvioFichadasPendientes()
        {
            if (Volatile.Read(ref recursosLiberados) != 0 || envioPendientes == null)
            {
                return;
            }

            try
            {
                ResultadoEnvioFichadas resultado = envioPendientes.Ejecutar();
                if (resultado.Estado == EstadoEnvioFichadas.Confirmado)
                {
                    politicaFichadas.RegistrarRecuperacion();
                    AplicarIntervalosEnUi();
                    if (resultado.Cantidad > 0)
                    {
                        MostrarNotificacion(
                            "Sincronización Exitosa",
                            resultado.Cantidad + " fichada(s) enviadas al servidor.",
                            ToolTipIcon.Info);
                        fichadaRepository.LimpiarFichadasSincronizadas();
                    }
                }
                else if (resultado.Estado == EstadoEnvioFichadas.FalloTemporal)
                {
                    politicaFichadas.RegistrarFalloTemporal();
                    AplicarIntervalosEnUi();
                    AgentLog.Info("Envio de fichadas diferido por un fallo temporal.");
                }
                else if (resultado.Estado == EstadoEnvioFichadas.Fallo)
                {
                    AgentLog.Info("Envio de fichadas no confirmado.");
                }
            }
            catch (Exception ex)
            {
                politicaFichadas.RegistrarFalloTemporal();
                AplicarIntervalosEnUi();
                AgentLog.Error("Envio de fichadas con error: " + ex.GetType().Name + ".");
            }
        }

        private void InicializarHeartbeat()
        {
            gestorIcono.RegistrarTimer();
            heartbeatTimer = new System.Windows.Forms.Timer();
            heartbeatTimer.Interval = politicaHeartbeat.IntervaloMs;
            heartbeatTimer.Tick += (sender, e) => EjecutarHeartbeatPeriodico();
            heartbeatTimer.Start();
            EjecutarHeartbeatPeriodico();
        }

        private void EjecutarHeartbeatPeriodico()
        {
            if (Volatile.Read(ref recursosLiberados) != 0)
            {
                return;
            }

            if (!heartbeatExclusivo.TryEnter())
            {
                return;
            }

            try
            {
                ColaTrabajoAgente.Encolar(() =>
                {
                    try
                    {
                        if (Volatile.Read(ref recursosLiberados) != 0)
                        {
                            return;
                        }

                        ResultadoCanal heartbeat = _apiClient.EnviarHeartbeatDetallado();
                        if (heartbeat.Aceptado)
                        {
                            apiConectada = true;
                            notificoCredenciales = false;
                            politicaHeartbeat.RegistrarRecuperacion();
                        }
                        else if (_apiClient.CredencialesRechazadas)
                        {
                            apiConectada = false;
                            NotificarCredencialesUnaVez();
                        }
                        else
                        {
                            apiConectada = false;
                            if (CicloVidaAgente.EsFalloTemporal(heartbeat.Motivo))
                            {
                                politicaHeartbeat.RegistrarFalloTemporal();
                            }
                        }

                        AplicarIntervalosEnUi();
                        PublicarIconoEnUi();
                    }
                    catch (Exception ex)
                    {
                        AgentLog.Error("Heartbeat con error: " + ex.GetType().Name + ".");
                        politicaHeartbeat.RegistrarFalloTemporal();
                        AplicarIntervalosEnUi();
                    }
                    finally
                    {
                        heartbeatExclusivo.Salir();
                    }
                });
            }
            catch (Exception ex)
            {
                heartbeatExclusivo.Salir();
                AgentLog.Error("Heartbeat con error: " + ex.GetType().Name + ".");
            }
        }

        private void OnBarraTareasCreada(object sender, EventArgs e)
        {
            if (Volatile.Read(ref recursosLiberados) != 0)
            {
                return;
            }

            int timers = gestorIcono.Timers;
            int iconos = gestorIcono.IconosCreados;
            AgentLog.Info(RegistroOperativo.TaskbarCreada());
            gestorIcono.Republicar();
            if (gestorIcono.Timers != timers || gestorIcono.IconosCreados != iconos)
            {
                return;
            }

            RepublicarIcono();
            AgentLog.Info(RegistroOperativo.IconoRepublicado());
        }

        private void MostrarSenalDeOtraInstancia()
        {
            Action avisar = () =>
            {
                if (Volatile.Read(ref recursosLiberados) != 0 || notifyIcon == null)
                {
                    return;
                }

                MostrarNotificacion("Control de Accesos", "El agente ya está en ejecución.", ToolTipIcon.Info);
            };

            if (lectorFormularioOculto != null && lectorFormularioOculto.IsHandleCreated && lectorFormularioOculto.InvokeRequired)
            {
                lectorFormularioOculto.BeginInvoke(avisar);
            }
            else
            {
                avisar();
            }
        }

        private void ReintentarConexion(object sender, EventArgs e)
        {
            if (!reintentoManual.Iniciar(EjecutarReintentoConexion))
            {
                AgentLog.Info("Reintento de conexion omitido: ya hay uno en curso.");
            }
        }

        private void EjecutarReintentoConexion()
        {
            try
            {
                AgentLog.Info("Reintento de conexion iniciado.");
                if (_apiClient != null && !_apiClient.TieneSesion)
                {
                    string clientId;
                    string clientSecret;
                    if (CredentialStorage.CargarCredenciales(out clientId, out clientSecret))
                    {
                        _apiClient.IniciarSesionAgente(clientId, clientSecret);
                    }
                }

                ResultadoCanal heartbeat = _apiClient == null
                    ? ResultadoCanal.Fallo("sin-autorizacion")
                    : _apiClient.EnviarHeartbeatDetallado();
                if (heartbeat.Aceptado)
                {
                    apiConectada = true;
                    notificoCredenciales = false;
                    politicaHeartbeat.RegistrarRecuperacion();
                }
                else if (_apiClient != null && _apiClient.CredencialesRechazadas)
                {
                    apiConectada = false;
                    NotificarCredencialesUnaVez();
                }
                else
                {
                    apiConectada = false;
                    if (CicloVidaAgente.EsFalloTemporal(heartbeat.Motivo))
                    {
                        politicaHeartbeat.RegistrarFalloTemporal();
                    }
                }

                EjecutarEnvioFichadasPendientes();
                AplicarIntervalosEnUi();
                PublicarIconoEnUi();
                AgentLog.Info("Reintento de conexion finalizado.");
            }
            catch (Exception ex)
            {
                AgentLog.Error("Reintento de conexion con error: " + ex.GetType().Name + ".");
            }
        }

        private void AbrirCarpetaLogs(object sender, EventArgs e)
        {
            try
            {
                string carpeta = AgentLog.CarpetaLogs();
                Directory.CreateDirectory(carpeta);
                Process.Start(new ProcessStartInfo
                {
                    FileName = carpeta,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                AgentLog.Error("Carpeta de logs no disponible: " + ex.GetType().Name + ".");
            }
        }

        private void RepublicarIcono()
        {
            if (Volatile.Read(ref recursosLiberados) != 0 || notifyIcon == null)
            {
                return;
            }

            CicloVidaAgente.RepublicarIcono(
                () => notifyIcon.Visible = false,
                () =>
                {
                    notifyIcon.Icon = Properties.Resources.circularColor;
                    notifyIcon.Visible = true;
                    PublicarIcono();
                });
        }

        private void PublicarIconoEnUi()
        {
            if (lectorFormularioOculto == null || lectorFormularioOculto.IsDisposed)
            {
                return;
            }

            if (lectorFormularioOculto.InvokeRequired)
            {
                lectorFormularioOculto.BeginInvoke(new Action(PublicarIcono));
            }
            else
            {
                PublicarIcono();
            }
        }

        private void PublicarIcono()
        {
            if (notifyIcon == null || Volatile.Read(ref recursosLiberados) != 0)
            {
                return;
            }

            EstadoOperativo estado = CicloVidaAgente.ResolverEstado(
                _apiClient != null && _apiClient.CredencialesRechazadas,
                sincronizando,
                apiConectada,
                lectorDisponible);
            string texto = CicloVidaAgente.TextoEstado(estado);
            notifyIcon.Text = texto;
            if (menuEstado != null)
            {
                menuEstado.Text = texto;
            }

            if (menuSincronizacion != null)
            {
                menuSincronizacion.Text = CicloVidaAgente.TextoUltimaSincronizacion(
                    resumenCatalogo == null ? (DateTime?)null : resumenCatalogo.UltimaSincronizacionCorrecta);
            }
        }

        private void AplicarIntervalosEnUi()
        {
            if (lectorFormularioOculto == null || lectorFormularioOculto.IsDisposed)
            {
                return;
            }

            Action aplicar = () =>
            {
                AsignarIntervalo(heartbeatTimer, politicaHeartbeat.IntervaloMs);
                AsignarIntervalo(syncTimer, politicaCatalogo.IntervaloMs);
                AsignarIntervalo(fichadasTimer, politicaFichadas.IntervaloMs);
            };

            if (lectorFormularioOculto.InvokeRequired)
            {
                lectorFormularioOculto.BeginInvoke(aplicar);
            }
            else
            {
                aplicar();
            }
        }

        private static void AsignarIntervalo(System.Windows.Forms.Timer timer, int intervaloMs)
        {
            if (timer == null || timer.Interval == intervaloMs)
            {
                return;
            }

            timer.Interval = intervaloMs;
        }

        private void NotificarCredencialesUnaVez()
        {
            if (notificoCredenciales)
            {
                return;
            }

            notificoCredenciales = true;
            Action avisar = () => MostrarNotificacion(
                "Credenciales no válidas",
                "Usá Vincular o cambiar credenciales para corregirlas.",
                ToolTipIcon.Warning);
            if (lectorFormularioOculto != null && lectorFormularioOculto.InvokeRequired)
            {
                lectorFormularioOculto.BeginInvoke(avisar);
            }
            else
            {
                avisar();
            }
        }
    }
}