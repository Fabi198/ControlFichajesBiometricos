using DevsFingerPrint.Domain.Interfaces;
using DevsFingerPrint.Domain.Models;
using DevsFingerPrint.Infrastructure.Data;
using DevsFingerPrint.Infrastructure.Repositories;
using DevsFingerPrint.Infrastructure.Services;
using DPUruNet;
using System;
using System.Collections.Generic;
using System.Diagnostics.Eventing.Reader;
using System.Drawing;
using System.Linq;
using System.Text;
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

        private readonly System.Windows.Forms.Timer syncTimer;
        private const int EMPRESA_ID = 1; // Reemplazar con el ID real de la empresa

        public MainTrayContext(ApiClient apiClient)
        {
            LocalDatabase.Inicializar();

            fichadaRepository = new FichadaRepository();
            biometricService = new BiometricService();
            _apiClient = apiClient;


            //METODO PARA SIMULAR
            var itemSimularFichada = new ToolStripMenuItem("Simular Fichada de Prueba", null, SimularFichada_Click);
            

            var contextMenu = new ContextMenuStrip();
            contextMenu.Items.Add("Enrolar nuevo empleado", null, EnrolarNuevoEmpleado);
            contextMenu.Items.Add("-");
            contextMenu.Items.Add("Estado del Lector", null, MostrarEstadoLector);
            contextMenu.Items.Add("-");
            //METODO PARA SIMULAR
            contextMenu.Items.Add(itemSimularFichada);
            contextMenu.Items.Add("-");
            //HASTA ACA
            contextMenu.Items.Add("Salir", null, SalirAplicacion);

            notifyIcon = new NotifyIcon
            {
                Icon = SystemIcons.Application, // Reemplazar despues por el icono de verdad
                ContextMenuStrip = contextMenu,
                Visible = true,
                Text = "DevsFingerPrint - Control de Fichajes"
            };

            
            biometricService.OnHuellaCapturada += OnHuellaCapturada;
            biometricService.OnEmpleadoIdentificado += OnEmpleadoIdentificado;
            biometricService.OnHuellaNoReconocida += OnHuellaNoReconocida;
            biometricService.OnEstadoCambiado += OnEstadoCambiado;

            
            CargarHuellasLocales();
            biometricService.IniciarLectura();


            syncTimer = new System.Windows.Forms.Timer();
            //syncTimer.Interval = 60 * 60 * 1000; // Cada 1 hora
            syncTimer.Interval = 30000; // Cada 30 segundos
            syncTimer.Tick += (s, e) => SincronizarConServidorAsync();
            syncTimer.Start();

            // Sincronización inicial al iniciar la aplicación
            SincronizarConServidorAsync();




            


        }

        private void SincronizarConServidorAsync()
        {
            
            System.Threading.ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                   
                    var catalogo = _apiClient.ObtenerCatalogo(EMPRESA_ID);
                    if (catalogo != null && catalogo.Empleados != null && catalogo.Huellas != null)
                    {
                        fichadaRepository.SincronizarCatalogoEmpresa(catalogo.Empleados, catalogo.Huellas);
                        CargarHuellasLocales(); // Actualiza la caché en memoria para el BiometricService
                    }


                    var pendientes = fichadaRepository.ObtenerFichadasPendientes();
                    var listaPendientes = new List<Fichada>(pendientes);

                    if (listaPendientes.Count > 0)
                    {
                        bool enviadas = _apiClient.EnviarFichadas(listaPendientes);
                        if (enviadas)
                        {
                            var ids = listaPendientes.ConvertAll(f => f.Id);
                            fichadaRepository.MarcarComoSincronizadas(ids);
                            MostrarNotificacion("Sincronización Exitosa", $"{listaPendientes.Count} fichada(s) enviadas al servidor.", ToolTipIcon.Info);
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error durante sincronización: {ex.Message}");
                }
            });
        }

        private void CargarHuellasLocales()
        {
            huellasCargadas = new List<Huella>(fichadaRepository.ObtenerHuellasLocales());
            MostrarNotificacion("Sistema listo", $"Cargadas {huellasCargadas.Count} huellas para verificación offline.", ToolTipIcon.Info);
        }

        private void OnHuellaCapturada(Fmd fmdCapturado)
        {
            
            biometricService.IdentificarEmpleado(fmdCapturado, huellasCargadas);
        }

        private void OnEmpleadoIdentificado(int empleadoId)
        {
            
            string tipoRegistro = "Entrada";

            var nuevaFichada = new Fichada
            {
                EmpleadoId = empleadoId,
                FechaHora = DateTime.Now,
                TipoRegistro = tipoRegistro,
                Metodo = "Biometrico",
                Sincronizado = false
            };

            fichadaRepository.GuardarFichadaLocal(nuevaFichada);

            MostrarNotificacion("Fichada Registrada", $"Empleado ID: {empleadoId} - {tipoRegistro} a las {nuevaFichada.FechaHora:HH:mm:ss}", ToolTipIcon.Info);
        }

        private void OnHuellaNoReconocida()
        {
            MostrarNotificacion("Acceso Denegado", "Huella no reconocida o no registrada.", ToolTipIcon.Warning);
        }

        private void OnEstadoCambiado(string mensaje)
        {
            string textoCompleto = $"DevsFingerPrint: {mensaje}";
            if (textoCompleto.Length > 63)
            {
                notifyIcon.Text = textoCompleto.Substring(0, 60) + "...";
            }
            else
            {
                notifyIcon.Text = textoCompleto;
            }
        }

        private void MostrarNotificacion(string titulo, string mensaje, ToolTipIcon icono)
        {
            notifyIcon.ShowBalloonTip(3000, titulo, mensaje, icono);
        }

        private void MostrarEstadoLector(object sender, EventArgs e)
        {
            MessageBox.Show($"Huellas en caché: {huellasCargadas.Count}\nBase de datos local: Lista", "DevsFingerPrint - Estado", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void EnrolarNuevoEmpleado(object sender, EventArgs e)
        {
            
            biometricService.DetenerLectura();

            Reader lectorFisico = biometricService.ObtenerLectorActual(); // Retorna la instancia de DPUruNet.Reader

            /*
            if (lectorFisico == null)
            {
                MessageBox.Show("No hay ningún lector U.are.U 4500 conectado.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                biometricService.IniciarLectura();
                return;
            }
            */

            // 2. Abrir el formulario modal de captura
            // 1. Obtener los empleados (de la API o SQLite) para cargar el desplegable
            List<Empleado> listaEmpleados = _apiClient.ObtenerEmpleados();

            using (var frmEnrolar = new EnrolarHuellaForm(lectorFisico, listaEmpleados))
            {
                if (frmEnrolar.ShowDialog() == DialogResult.OK && frmEnrolar.HuellaCapturada != null)
                {
                    System.Diagnostics.Debug.WriteLine($"Entro por aca. Data: {frmEnrolar.HuellaCapturada.TemplateBiometrico}");

                    // A. Guardar localmente en SQLite
                    Huella nuevaHuella = frmEnrolar.HuellaCapturada;
                    int indiceDedo = frmEnrolar.IndiceDedoSeleccionado;

                    // B. Intentar subir la huella a la API en segundo plano
                    ThreadPool.QueueUserWorkItem(_ =>
                    {
                        // 🔑 Se pasa la huella y el dedo seleccionado
                        bool subida = _apiClient.GuardarHuella(nuevaHuella, indiceDedo);

                        if (subida)
                        {
                            MostrarNotificacion("Enrolamiento", "La huella fue subida correctamente al servidor central.", ToolTipIcon.Info);
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine("Algo esta saliendo mal al subir la huella a la API.");
                        }
                    });

                    CargarHuellasLocales();
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("Se canceló el enrolamiento o no se capturó la huella.");
                }
            }

            biometricService.IniciarLectura();
        }

        private void SalirAplicacion(object sender, EventArgs e)
        {
            biometricService.DetenerLectura();
            notifyIcon.Visible = false;
            Application.Exit();
        }



        // METODO FALSO PARA SIMULAR

        private void SimularFichada_Click(object sender, EventArgs e)
        {
            string tipoRegistro = "Entrada";

            var nuevaFichada = new Fichada
            {
                EmpleadoId = 1,
                FechaHora = DateTime.Now,
                TipoRegistro = tipoRegistro,
                Metodo = "BiometricoFAKE",
                Sincronizado = false
            };

            fichadaRepository.GuardarFichadaLocal(nuevaFichada);

            MostrarNotificacion("Fichada Registrada", $"Empleado ID: {1} - {tipoRegistro} a las {nuevaFichada.FechaHora:HH:mm:ss}", ToolTipIcon.Info);
        }




    }
}
