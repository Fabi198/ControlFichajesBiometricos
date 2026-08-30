using DevsFingerPrint.Domain.Models;
using DPUruNet;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;
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

        public EnrolarHuellaForm(Reader reader, IEnumerable<Empleado> listaEmpleados)
        {
            _reader = reader;
            System.Diagnostics.Debug.WriteLine("[LOG EnrolarForm] Constructor iniciado.");

            InicializarComponentes();
            CargarEmpleados(listaEmpleados);
            CargarDedos();
            IniciarSecuenciaEnrolamiento();
        }

        private void InicializarComponentes()
        {
            System.Diagnostics.Debug.WriteLine("[LOG EnrolarForm] Inicializando componentes de UI...");
            this.Text = "Enrolar Nueva Huella - U.are.U 4500";
            this.Size = new Size(380, 520);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;

            // Panel Superior con Selectores
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

            // PictureBox central para previsualización
            pbHuella = new PictureBox
            {
                Dock = DockStyle.Fill,
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.Black
            };

            // Panel Inferior para Botones
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
                System.Diagnostics.Debug.WriteLine("[LOG EnrolarForm] Clic en botón Cancelar.");
                DetenerCaptura();
                this.DialogResult = DialogResult.Cancel;
            };

            pnlBotones.Controls.Add(btnSimular);
            pnlBotones.Controls.Add(btnCancelar);

            this.Controls.Add(pbHuella);
            this.Controls.Add(pnlSelectores);
            this.Controls.Add(pnlBotones);

            this.FormClosing += (s, e) => {
                System.Diagnostics.Debug.WriteLine($"[LOG EnrolarForm] FormClosing disparado. CloseReason: {e.CloseReason}");
                DetenerCaptura();
            };

            System.Diagnostics.Debug.WriteLine("[LOG EnrolarForm] Componentes de UI inicializados correctamente.");
        }

        private void CargarEmpleados(IEnumerable<Empleado> empleados)
        {
            if (empleados != null)
            {
                var lista = new List<Empleado>(empleados);
                System.Diagnostics.Debug.WriteLine($"[LOG EnrolarForm] Cargando {lista.Count} empleados en el ComboBox...");
                cbEmpleados.DisplayMember = "NombreCompleto";
                cbEmpleados.ValueMember = "Id";
                cbEmpleados.DataSource = lista;
                System.Diagnostics.Debug.WriteLine($"[LOG EnrolarForm] Empleados cargados. Seleccionado: {cbEmpleados.SelectedValue}");
            }
        }

        private void CargarDedos()
        {
            System.Diagnostics.Debug.WriteLine("[LOG EnrolarForm] Cargando lista de dedos en el ComboBox...");
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
            System.Diagnostics.Debug.WriteLine($"[LOG EnrolarForm] Validando selecciones -> Empleado: {cbEmpleados.SelectedValue}, Dedo: {cbDedos.SelectedValue}");

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
            if (_reader == null)
            {
                System.Diagnostics.Debug.WriteLine("[LOG EnrolarForm] ERROR CRÍTICO: La instancia de _reader recibida es NULL.");
                return;
            }

            System.Diagnostics.Debug.WriteLine($"[LOG EnrolarForm] Iniciando enrolamiento con lector Serial: {_reader.Description.SerialNumber}");
            _enrollmentFmds = new List<Fmd>();

            _reader.On_Captured += Reader_OnCaptured;

            Constants.ResultCode resOpen = _reader.Open(Constants.CapturePriority.DP_PRIORITY_EXCLUSIVE);
            System.Diagnostics.Debug.WriteLine($"[LOG EnrolarForm] Resultado de Open (EXCLUSIVE): {resOpen}");

            if (resOpen != Constants.ResultCode.DP_SUCCESS)
            {
                resOpen = _reader.Open(Constants.CapturePriority.DP_PRIORITY_COOPERATIVE);
                System.Diagnostics.Debug.WriteLine($"[LOG EnrolarForm] Resultado de Open (COOPERATIVE): {resOpen}");
            }

            if (resOpen == Constants.ResultCode.DP_SUCCESS)
            {
                SolicitarSiguienteMuestra();
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"[LOG EnrolarForm] ERROR: No se pudo abrir el lector. ResultCode: {resOpen}");
            }
        }

        private void SolicitarSiguienteMuestra()
        {
            if (_reader != null)
            {
                System.Diagnostics.Debug.WriteLine("[LOG EnrolarForm] Ejecutando CaptureAsync...");
                Constants.ResultCode resCapture = _reader.CaptureAsync(
                    Constants.Formats.Fid.ANSI,
                    Constants.CaptureProcessing.DP_IMG_PROC_DEFAULT,
                    _reader.Capabilities.Resolutions[0]);
                System.Diagnostics.Debug.WriteLine($"[LOG EnrolarForm] Resultado de CaptureAsync: {resCapture}");
            }
        }

        private void Reader_OnCaptured(CaptureResult captureResult)
        {
            System.Diagnostics.Debug.WriteLine($"\n[LOG EnrolarForm] --- Evento Reader_OnCaptured Disparado ---");
            System.Diagnostics.Debug.WriteLine($"[LOG EnrolarForm] ResultCode: {captureResult.ResultCode}, Quality: {captureResult.Quality}");

            if (captureResult.ResultCode != Constants.ResultCode.DP_SUCCESS || captureResult.Data == null)
            {
                System.Diagnostics.Debug.WriteLine($"[LOG EnrolarForm] Captura desestimada por ResultCode inválido o Data nula.");
                SolicitarSiguienteMuestra();
                return;
            }

            // Actualizar vista previa de forma asíncrona
            this.BeginInvoke(new Action(() =>
            {
                foreach (Fid.Fiv fiv in captureResult.Data.Views)
                {
                    Bitmap oldBmp = pbHuella.Image as Bitmap;
                    pbHuella.Image = CrearBitmap(fiv.RawImage, fiv.Width, fiv.Height);
                    if (oldBmp != null) oldBmp.Dispose();
                    break;
                }
            }));

            // Convertir a FMD utilizando el formato ANSI
            DataResult<Fmd> resultConversion = FeatureExtraction.CreateFmdFromFid(captureResult.Data, Constants.Formats.Fmd.ANSI);
            System.Diagnostics.Debug.WriteLine($"[LOG EnrolarForm] Resultado conversión FID -> FMD: {resultConversion.ResultCode}");

            if (resultConversion.ResultCode == Constants.ResultCode.DP_SUCCESS)
            {
                _enrollmentFmds.Add(resultConversion.Data);
                System.Diagnostics.Debug.WriteLine($"[LOG EnrolarForm] Muestra guardada. Total muestras actuales: {_enrollmentFmds.Count} / 4");

                this.BeginInvoke(new Action(() =>
                {
                    lblInstrucciones.Text = $"Muestra registrada ({_enrollmentFmds.Count} de 4). Vuelva a apoyar.";
                }));

                if (_enrollmentFmds.Count >= 4)
                {
                    System.Diagnostics.Debug.WriteLine("[LOG EnrolarForm] Muestras suficientes (4/4). Generando plantilla final...");
                    DataResult<Fmd> createResult = Enrollment.CreateEnrollmentFmd(Constants.Formats.Fmd.ANSI, _enrollmentFmds);
                    System.Diagnostics.Debug.WriteLine($"[LOG EnrolarForm] Resultado de CreateEnrollmentFmd: {createResult.ResultCode}");

                    if (createResult.ResultCode == Constants.ResultCode.DP_SUCCESS)
                    {
                        this.BeginInvoke(new Action(() =>
                        {
                            System.Diagnostics.Debug.WriteLine("[LOG EnrolarForm] Enrolamiento exitoso. Validando selecciones...");
                            if (!ValidarSelecciones())
                            {
                                _enrollmentFmds.Clear();
                                lblInstrucciones.Text = "Coloque el dedo en el lector (Muestra 1 de 4)";
                                SolicitarSiguienteMuestra();
                                return;
                            }

                            int empId = Convert.ToInt32(cbEmpleados.SelectedValue);
                            int dedoId = Convert.ToInt32(cbDedos.SelectedValue);

                            // Obtener los bytes directamente desde la propiedad Bytes del FMD generado
                            byte[] fmdBytes = createResult.Data.Bytes;

                            if (fmdBytes != null && fmdBytes.Length > 0)
                            {
                                string templateBase64 = Convert.ToBase64String(fmdBytes);

                                EmpleadoIdSeleccionado = empId;
                                IndiceDedoSeleccionado = dedoId;

                                HuellaCapturada = new Huella
                                {
                                    EmpleadoId = empId,
                                    IndiceDedo = dedoId,
                                    TemplateBiometrico = templateBase64
                                };

                                System.Diagnostics.Debug.WriteLine($"[LOG EnrolarForm] Huella compilada y asignada. EmpleadoId: {empId}, DedoId: {dedoId}");

                                ThreadPool.QueueUserWorkItem(_ =>
                                {
                                    DetenerCaptura();

                                    this.BeginInvoke(new Action(() =>
                                    {
                                        MessageBox.Show("¡Huella enrolada exitosamente!", "Éxito", MessageBoxButtons.OK, MessageBoxIcon.Information);
                                        this.DialogResult = DialogResult.OK;
                                        this.Close();
                                    }));
                                });
                            }
                            else
                            {
                                MessageBox.Show("La plantilla generada está vacía.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                _enrollmentFmds.Clear();
                                SolicitarSiguienteMuestra();
                            }
                        }));
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"[LOG EnrolarForm] ERROR: Falló CreateEnrollmentFmd: {createResult.ResultCode}");
                        this.BeginInvoke(new Action(() =>
                        {
                            MessageBox.Show("No se pudo compilar la plantilla (huellas disímiles). Intente nuevamente.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            _enrollmentFmds.Clear();
                            lblInstrucciones.Text = "Coloque el dedo en el lector (Muestra 1 de 4)";
                            SolicitarSiguienteMuestra();
                        }));
                    }
                }
                else
                {
                    SolicitarSiguienteMuestra();
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"[LOG EnrolarForm] ERROR al extraer características de la muestra: {resultConversion.ResultCode}");
                SolicitarSiguienteMuestra();
            }
        }

        private void DetenerCaptura()
        {
            if (_reader != null)
            {
                System.Diagnostics.Debug.WriteLine("[LOG EnrolarForm] Ejecutando DetenerCaptura()...");
                try
                {
                    _reader.On_Captured -= Reader_OnCaptured;
                    _reader.CancelCapture();
                    _reader.Dispose();
                    System.Diagnostics.Debug.WriteLine("[LOG EnrolarForm] Lector detenido y cerrado correctamente.");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[LOG EnrolarForm] Excepción en DetenerCaptura: {ex.Message}");
                }
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
            System.Diagnostics.Debug.WriteLine("[LOG EnrolarForm] Clic en botón Simular Huella.");

            if (!ValidarSelecciones())
                return;

            ThreadPool.QueueUserWorkItem(_ =>
            {
                DetenerCaptura();

                this.BeginInvoke(new Action(() =>
                {
                    int empId = Convert.ToInt32(cbEmpleados.SelectedValue);
                    int dedoId = Convert.ToInt32(cbDedos.SelectedValue);

                    string fakeTemplateBase64 = "<FMD><Bytes>AQAAAFQAAABM...</Bytes></FMD>";

                    EmpleadoIdSeleccionado = empId;
                    IndiceDedoSeleccionado = dedoId;

                    HuellaCapturada = new Huella
                    {
                        EmpleadoId = empId,
                        IndiceDedo = dedoId,
                        TemplateBiometrico = fakeTemplateBase64
                    };

                    MessageBox.Show("¡Huella SIMULADA enrolada con éxito!", "Modo Test", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    this.DialogResult = DialogResult.OK;
                    this.Close();
                }));
            });
        }
    }
}