using DevsFingerPrint.Domain.Models;
using DPUruNet;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace DevsFingerPrint.Presentation
{
    public class EnrolarHuellaForm : Form
    {
        private Reader _reader;
        private List<Fmd> _enrollmentFmds;

        // Propiedades para devolver la información al formulario principal
        public Huella HuellaCapturada { get; private set; }
        public int EmpleadoIdSeleccionado { get; private set; }
        public int IndiceDedoSeleccionado { get; private set; }

        // Controles de UI
        private ComboBox cbEmpleados;
        private ComboBox cbDedos;
        private Label lblInstrucciones;
        private PictureBox pbHuella;
        private Button btnCancelar;
        private Button btnSimular;

        // Recibe la lista de empleados para poblar el selector y el lector biométrico
        public EnrolarHuellaForm(Reader reader, IEnumerable<Empleado> listaEmpleados)
        {
            _reader = reader;

            InicializarComponentes();
            CargarEmpleados(listaEmpleados);
            CargarDedos();
            IniciarSecuenciaEnrolamiento();
        }

        private void InicializarComponentes()
        {
            this.Text = "Enrolar Nueva Huella - U.are.U 4500";
            this.Size = new Size(380, 520);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;

            // 1. Panel Superior con Selectores (Empleado y Dedo)
            Panel pnlSelectores = new Panel
            {
                Dock = DockStyle.Top,
                Height = 110,
                Padding = new Padding(10)
            };

            Label lblEmpleado = new Label { Text = "Empleado:", Location = new Point(10, 12), AutoSize = true };
            cbEmpleados = new ComboBox
            {
                Location = new Point(100, 8),
                Width = 240,
                DropDownStyle = ComboBoxStyle.DropDownList
            };

            Label lblDedo = new Label { Text = "Dedo:", Location = new Point(10, 47), AutoSize = true };
            cbDedos = new ComboBox
            {
                Location = new Point(100, 44),
                Width = 240,
                DropDownStyle = ComboBoxStyle.DropDownList
            };

            lblInstrucciones = new Label
            {
                Text = "Coloque el dedo en el lector (Muestra 1 de 4)",
                Location = new Point(10, 80),
                Size = new Size(340, 25),
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 9, FontStyle.Bold)
            };

            pnlSelectores.Controls.Add(lblEmpleado);
            pnlSelectores.Controls.Add(cbEmpleados);
            pnlSelectores.Controls.Add(lblDedo);
            pnlSelectores.Controls.Add(cbDedos);
            pnlSelectores.Controls.Add(lblInstrucciones);

            // 2. PictureBox central para previsualización
            pbHuella = new PictureBox
            {
                Dock = DockStyle.Fill,
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.Black
            };

            // 3. Panel Inferior para Botones
            Panel pnlBotones = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 50,
                Padding = new Padding(8)
            };

            btnSimular = new Button
            {
                Text = "🧪 Simular Huella",
                Dock = DockStyle.Left,
                Width = 160,
                BackColor = Color.LightSkyBlue,
                FlatStyle = FlatStyle.Flat
            };
            btnSimular.Click += btnSimularLectura_Click;

            btnCancelar = new Button
            {
                Text = "Cancelar",
                Dock = DockStyle.Right,
                Width = 160
            };
            btnCancelar.Click += (s, e) => {
                DetenerCaptura();
                this.DialogResult = DialogResult.Cancel;
            };

            pnlBotones.Controls.Add(btnSimular);
            pnlBotones.Controls.Add(btnCancelar);

            // Agregar paneles al Formulario
            this.Controls.Add(pbHuella);
            this.Controls.Add(pnlSelectores);
            this.Controls.Add(pnlBotones);

            this.FormClosing += (s, e) => DetenerCaptura();
        }

        private void CargarEmpleados(IEnumerable<Empleado> empleados)
        {
            if (empleados != null)
            {
                cbEmpleados.DisplayMember = "NombreCompleto"; // Ajustar al nombre de propiedad de la entidad Empleado
                cbEmpleados.ValueMember = "Id";
                cbEmpleados.DataSource = empleados;
            }
        }

        private void CargarDedos()
        {
            // Mapeo estándar de dedos (1 a 10)
            var listaDedos = new[]
            {
                new { Id = 1, Nombre = "Pulgar Derecho" },
                new { Id = 2, Nombre = "Indice Derecho" },
                new { Id = 3, Nombre = "Medio Derecho" },
                new { Id = 4, Nombre = "Anular Derecho" },
                new { Id = 5, Nombre = "Meñique Derecho" },
                new { Id = 6, Nombre = "Pulgar Izquierdo" },
                new { Id = 7, Nombre = "Indice Izquierdo" },
                new { Id = 8, Nombre = "Medio Izquierdo" },
                new { Id = 9, Nombre = "Anular Izquierdo" },
                new { Id = 10, Nombre = "Meñique Izquierdo" }
            };

            cbDedos.DisplayMember = "Nombre";
            cbDedos.ValueMember = "Id";
            cbDedos.DataSource = listaDedos;
        }

        private bool ValidarSelecciones()
        {
            if (cbEmpleados.SelectedValue == null)
            {
                MessageBox.Show("Por favor, seleccione un empleado.", "Atención", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }

            if (cbDedos.SelectedValue == null)
            {
                MessageBox.Show("Por favor, seleccione un dedo.", "Atención", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }

            return true;
        }

        private void IniciarSecuenciaEnrolamiento()
        {
            if (_reader == null) return;

            _enrollmentFmds = new List<Fmd>();
            _reader.On_Captured += Reader_OnCaptured;
            _reader.Open(Constants.CapturePriority.DP_PRIORITY_EXCLUSIVE);
            _reader.StartStreaming();
        }

        private void Reader_OnCaptured(CaptureResult captureResult)
        {
            if (captureResult.ResultCode != Constants.ResultCode.DP_SUCCESS || captureResult.Data == null)
                return;

            foreach (Fid.Fiv fiv in captureResult.Data.Views)
            {
                pbHuella.Image = CrearBitmap(fiv.RawImage, fiv.Width, fiv.Height);
                break;
            }

            DataResult<Fmd> resultConversion = FeatureExtraction.CreateFmdFromFid(captureResult.Data, Constants.Formats.Fmd.DP_VERIFICATION);

            if (resultConversion.ResultCode == Constants.ResultCode.DP_SUCCESS)
            {
                _enrollmentFmds.Add(resultConversion.Data);

                this.Invoke(new Action(() =>
                {
                    lblInstrucciones.Text = $"Muestra registrada ({_enrollmentFmds.Count} de 4). Vuelva a apoyar.";
                }));

                if (_enrollmentFmds.Count >= 4)
                {
                    DataResult<Fmd> createResult = Enrollment.CreateEnrollmentFmd(Constants.Formats.Fmd.DP_VERIFICATION, _enrollmentFmds);

                    if (createResult.ResultCode == Constants.ResultCode.DP_SUCCESS)
                    {
                        this.Invoke(new Action(() =>
                        {
                            if (!ValidarSelecciones())
                            {
                                _enrollmentFmds.Clear();
                                lblInstrucciones.Text = "Coloque el dedo en el lector (Muestra 1 de 4)";
                                return;
                            }

                            DetenerCaptura();

                            int empId = Convert.ToInt32(cbEmpleados.SelectedValue);
                            int dedoId = Convert.ToInt32(cbDedos.SelectedValue);

                            string templateBase64 = Fmd.SerializeXml(createResult.Data);

                            EmpleadoIdSeleccionado = empId;
                            IndiceDedoSeleccionado = dedoId;

                            HuellaCapturada = new Huella
                            {
                                EmpleadoId = empId,
                                TemplateBiometrico = templateBase64
                            };

                            MessageBox.Show("¡Huella enrolada exitosamente!", "Éxito", MessageBoxButtons.OK, MessageBoxIcon.Information);
                            this.DialogResult = DialogResult.OK;
                            this.Close();
                        }));
                    }
                    else
                    {
                        this.Invoke(new Action(() =>
                        {
                            MessageBox.Show("No se pudo crear la plantilla de enrolamiento. Intente de nuevo.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            _enrollmentFmds.Clear();
                            lblInstrucciones.Text = "Coloque el dedo en el lector (Muestra 1 de 4)";
                        }));
                    }
                }
            }
        }

        private void DetenerCaptura()
        {
            if (_reader != null)
            {
                _reader.On_Captured -= Reader_OnCaptured;
                _reader.StopStreaming();
            }
        }

        private Bitmap CrearBitmap(byte[] bytes, int width, int height)
        {
            Bitmap bmp = new Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format8bppIndexed);
            System.Drawing.Imaging.ColorPalette palette = bmp.Palette;
            for (int i = 0; i < 256; i++) palette.Entries[i] = Color.FromArgb(i, i, i);
            bmp.Palette = palette;

            System.Drawing.Imaging.BitmapData bmpData = bmp.LockBits(
                new Rectangle(0, 0, width, height),
                System.Drawing.Imaging.ImageLockMode.WriteOnly,
                bmp.PixelFormat);

            System.Runtime.InteropServices.Marshal.Copy(bytes, 0, bmpData.Scan0, bytes.Length);
            bmp.UnlockBits(bmpData);

            return bmp;
        }

        private void btnSimularLectura_Click(object sender, EventArgs e)
        {
            if (!ValidarSelecciones())
                return;

            DetenerCaptura();

            int empId = Convert.ToInt32(cbEmpleados.SelectedValue);
            int dedoId = Convert.ToInt32(cbDedos.SelectedValue);

            string fakeTemplateBase64 = "<FMD><Bytes>AQAAAFQAAABM...</Bytes></FMD>";

            EmpleadoIdSeleccionado = empId;
            IndiceDedoSeleccionado = dedoId;

            HuellaCapturada = new Huella
            {
                EmpleadoId = empId,
                TemplateBiometrico = fakeTemplateBase64
            };

            MessageBox.Show("¡Huella SIMULADA enrolada con éxito!", "Modo Test", MessageBoxButtons.OK, MessageBoxIcon.Information);
            this.DialogResult = DialogResult.OK;
            this.Close();
        }
    }
}