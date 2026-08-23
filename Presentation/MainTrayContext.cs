using DevsFingerPrint.Domain.Interfaces;
using DevsFingerPrint.Domain.Models;
using DevsFingerPrint.Infrastructure.Data;
using DevsFingerPrint.Infrastructure.Repositories;
using DevsFingerPrint.Infrastructure.Services;
using DPUruNet;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace DevsFingerPrint.Presentation
{
    public class MainTrayContext : ApplicationContext
    {

        private readonly NotifyIcon notifyIcon;
        private readonly IBiometricService biometricService;
        private readonly IFichadaRepository fichadaRepository;
        private List<Huella> huellasCargadas;

        public MainTrayContext()
        {
            LocalDatabase.Inicializar();

            fichadaRepository = new FichadaRepository();
            biometricService = new BiometricService();

            var contextMenu = new ContextMenuStrip();
            contextMenu.Items.Add("Estado del Lector", null, MostrarEstadoLector);
            contextMenu.Items.Add("-");
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

        private void SalirAplicacion(object sender, EventArgs e)
        {
            biometricService.DetenerLectura();
            notifyIcon.Visible = false;
            Application.Exit();
        }

    }
}
