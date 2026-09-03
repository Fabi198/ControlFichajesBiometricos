using DevsFingerPrint.Domain.DTO;
using DevsFingerPrint.Infrastructure.Services;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace DevsFingerPrint.Presentation
{
    public class SeleccionSucursalForm : Form
    {
        private ComboBox cmbSucursales;
        private Button btnGuardar;
        private Label lblTitulo;
        private bool seleccionExitosa = false;

        public SeleccionSucursalForm()
        {
            System.Diagnostics.Debug.WriteLine("[SeleccionSucursalForm] -> Inicializando constructor del formulario de selección.");
            InitializeComponentCustom();
            System.Diagnostics.Debug.WriteLine("[SeleccionSucursalForm] -> Constructor inicializado correctamente.");
        }

        private void InitializeComponentCustom()
        {
            System.Diagnostics.Debug.WriteLine("[SeleccionSucursalForm] -> Configurando controles visuales (InitializeComponentCustom).");

            this.Text = "Selección de Sucursal";
            this.Size = new Size(380, 220);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;

            lblTitulo = new Label
            {
                Text = "Por favor, seleccione la sucursal en la que operará esta terminal:",
                Location = new Point(20, 20),
                Size = new Size(320, 40),
                Font = new Font("Segoe UI", 9.5f, FontStyle.Regular)
            };

            cmbSucursales = new ComboBox
            {
                Location = new Point(20, 75),
                Size = new Size(320, 25),
                DropDownStyle = ComboBoxStyle.DropDownList
            };

            btnGuardar = new Button
            {
                Text = "Guardar y Continuar",
                Location = new Point(20, 120),
                Size = new Size(320, 38),
                BackColor = Color.FromArgb(0, 122, 204),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold)
            };
            btnGuardar.Click += BtnGuardar_Click;

            this.Controls.Add(lblTitulo);
            this.Controls.Add(cmbSucursales);
            this.Controls.Add(btnGuardar);

            System.Diagnostics.Debug.WriteLine("[SeleccionSucursalForm] -> Controles añadidos exitosamente al formulario.");
        }

        public void CargarSucursales(List<SucursalDTO> listaSucursales)
        {
            System.Diagnostics.Debug.WriteLine("[SeleccionSucursalForm] -> Iniciando carga de sucursales en el ComboBox.");

            try
            {
                if (listaSucursales == null)
                {
                    System.Diagnostics.Debug.WriteLine("[SeleccionSucursalForm] ⚠️ ADVERTENCIA: La lista de sucursales provista es null.");
                    listaSucursales = new List<SucursalDTO>();
                }

                System.Diagnostics.Debug.WriteLine($"[SeleccionSucursalForm] -> Cantidad de sucursales recibidas para enlazar: {listaSucursales.Count}");

                cmbSucursales.DataSource = listaSucursales;
                cmbSucursales.DisplayMember = "Nombre";
                cmbSucursales.ValueMember = "Id";

                System.Diagnostics.Debug.WriteLine("[SeleccionSucursalForm] -> DataSource asignado correctamente al ComboBox.");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SeleccionSucursalForm] ❌ ERROR CRÍTICO al cargar sucursales: {ex.Message}");
                MessageBox.Show("Error al cargar las sucursales: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnGuardar_Click(object sender, EventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("[SeleccionSucursalForm] -> Click detectado en el botón 'Guardar y Continuar'.");

            if (cmbSucursales.SelectedItem == null)
            {
                System.Diagnostics.Debug.WriteLine("[SeleccionSucursalForm] ⚠️ Intento de guardado fallido: Ninguna sucursal seleccionada en el ComboBox.");
                MessageBox.Show("Debe seleccionar una sucursal.", "Atención", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            int sucursalIdSeleccionada = (int)cmbSucursales.SelectedValue;
            string sucursalNombreSeleccionada = cmbSucursales.Text;
            System.Diagnostics.Debug.WriteLine($"[SeleccionSucursalForm] -> Sucursal elegida por el usuario: ID = {sucursalIdSeleccionada}, Nombre = '{sucursalNombreSeleccionada}'");

            System.Diagnostics.Debug.WriteLine("[SeleccionSucursalForm] -> Leyendo configuración local actual...");
            var configActual = DispositivoConfigService.LeerConfiguracion() ?? new ConfiguracionLocalDto();

            System.Diagnostics.Debug.WriteLine($"[SeleccionSucursalForm] -> Configuración leída. Serial actual: '{configActual.SerialLector}', Guardando nueva sucursal ID: {sucursalIdSeleccionada}...");
            DispositivoConfigService.GuardarConfiguracion(sucursalIdSeleccionada, configActual.SerialLector);
            System.Diagnostics.Debug.WriteLine("[SeleccionSucursalForm] -> Configuración guardada exitosamente en disco.");

            seleccionExitosa = true;
            System.Diagnostics.Debug.WriteLine("[SeleccionSucursalForm] -> Cerrando formulario con éxito.");
            this.Close();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            base.OnFormClosing(e);
            System.Diagnostics.Debug.WriteLine($"[SeleccionSucursalForm] -> Evento OnFormClosing disparado. Reason: {e.CloseReason}, SeleccionExitosa: {seleccionExitosa}");

            if (!seleccionExitosa && e.CloseReason == CloseReason.UserClosing)
            {
                System.Diagnostics.Debug.WriteLine("[SeleccionSucursalForm] 🛑 El usuario cerró la ventana a la fuerza sin seleccionar sucursal. Forzando cierre completo de la aplicación (Application.Exit).");
                Application.Exit();
            }
        }
    }
}