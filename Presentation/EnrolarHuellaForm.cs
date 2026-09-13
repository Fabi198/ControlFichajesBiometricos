using DevsFingerPrint.Domain.Models;
using DPUruNet;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Threading;
using System.Windows.Forms;

namespace DevsFingerPrint.Presentation
{
    public class EnrolarHuellaForm : Form
    {
        private Reader _reader;
        private List<Fmd> _enrollmentFmds;

        public Huella HuellaCapturada { get; private set; }
        public int EmpleadoIdSeleccionado { get; private set; }
        public int IndiceDedoSeleccionado { get; private set; }

        // Colores idénticos a FormLogin
        private readonly Color ColorFondo = Color.FromArgb(10, 15, 29);       // Oscuro profundo
        private readonly Color ColorCard = Color.FromArgb(18, 26, 46);        // Tarjeta contenedora
        private readonly Color ColorBordeInput = Color.FromArgb(35, 48, 74);  // Borde de controles
        private readonly Color ColorAzul = Color.FromArgb(24, 103, 255);      // Azul vibrante
        private readonly Color ColorTextoSub = Color.FromArgb(140, 155, 185);  // Gris texto secundario

        // Controles de UI
        private ComboBox cbEmpleados;
        private ComboBox cbDedos;
        private Label lblInstrucciones;
        private PictureBox pbHuella;
        private Button btnSimular;
        private Button btnCancelar;

        public EnrolarHuellaForm(Reader reader, IEnumerable<Empleado> listaEmpleados)
        {
            _reader = reader;
            ConfigurarVentana();
            CrearControles();
            CargarEmpleados(listaEmpleados);
            CargarDedos();
            IniciarSecuenciaEnrolamiento();
        }

        private void ConfigurarVentana()
        {
            this.Text = "Enrolamiento Biométrico";
            this.Size = new Size(420, 560);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.BackColor = ColorFondo;
            this.DoubleBuffered = true;
        }

        private void CrearControles()
        {
            // Panel Tarjeta principal
            Panel cardPanel = new Panel
            {
                Size = new Size(340, 350),
                Location = new Point((this.ClientSize.Width - 340) / 2, 130),
                BackColor = Color.Transparent
            };
            cardPanel.Paint += CardPanel_Paint;

            // Label: Empleado
            Label lblEmpleado = new Label
            {
                Text = "Empleado",
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                Location = new Point(20, 20),
                AutoSize = true
            };

            // Input: Empleado ComboBox
            cbEmpleados = new ComboBox
            {
                Location = new Point(20, 42),
                Size = new Size(300, 30),
                BackColor = ColorFondo,
                ForeColor = Color.White,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 9.5f)
            };

            // Label: Dedo
            Label lblDedo = new Label
            {
                Text = "Dedo a Enrolar",
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                Location = new Point(20, 80),
                AutoSize = true
            };

            // Input: Dedo ComboBox
            cbDedos = new ComboBox
            {
                Location = new Point(20, 102),
                Size = new Size(300, 30),
                BackColor = ColorFondo,
                ForeColor = Color.White,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 9.5f)
            };

            // PictureBox para previsualización de huella integrado en la tarjeta
            pbHuella = new PictureBox
            {
                Location = new Point(135, 145),
                Size = new Size(70, 70),
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = ColorFondo
            };

            // Label: Instrucciones dinámicas
            lblInstrucciones = new Label
            {
                Text = "Coloque el dedo en el lector (Muestra 1 de 4)",
                ForeColor = ColorTextoSub,
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                Location = new Point(20, 225),
                Size = new Size(300, 20),
                TextAlign = ContentAlignment.MiddleCenter
            };

            // Botón: Simular
            btnSimular = new Button
            {
                Text = "Simular Huella",
                Location = new Point(20, 255),
                Size = new Size(142, 36),
                BackColor = ColorBordeInput,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnSimular.FlatAppearance.BorderSize = 0;
            btnSimular.Click += btnSimularLectura_Click;

            // Botón: Cancelar
            btnCancelar = new Button
            {
                Text = "Cancelar",
                Location = new Point(178, 255),
                Size = new Size(142, 36),
                BackColor = ColorBordeInput,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnCancelar.FlatAppearance.BorderSize = 0;
            btnCancelar.Click += (s, e) => {
                DetenerCaptura();
                this.DialogResult = DialogResult.Cancel;
            };

            cardPanel.Controls.Add(lblEmpleado);
            cardPanel.Controls.Add(cbEmpleados);
            cardPanel.Controls.Add(lblDedo);
            cardPanel.Controls.Add(cbDedos);
            cardPanel.Controls.Add(pbHuella);
            cardPanel.Controls.Add(lblInstrucciones);
            cardPanel.Controls.Add(btnSimular);
            cardPanel.Controls.Add(btnCancelar);

            this.Controls.Add(cardPanel);

            this.FormClosing += (s, e) => {
                DetenerCaptura();
            };
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            // 1. Badge superior "FP" redondeado
            int iconSize = 48;
            int iconX = (this.ClientSize.Width - iconSize) / 2;
            int iconY = 25;
            Rectangle iconRect = new Rectangle(iconX, iconY, iconSize, iconSize);

            using (GraphicsPath path = ObtenerRutaRedondeada(iconRect, 12))
            using (SolidBrush brush = new SolidBrush(ColorAzul))
            {
                g.FillPath(brush, path);
            }

            using (Font fontBadge = new Font("Segoe UI", 12f, FontStyle.Bold))
            using (SolidBrush textBrush = new SolidBrush(Color.White))
            {
                StringFormat sf = new StringFormat
                {
                    Alignment = StringAlignment.Center,
                    LineAlignment = StringAlignment.Center
                };
                g.DrawString("FP", fontBadge, textBrush, iconRect, sf);
            }

            // 2. Titular
            using (Font fontTitulo = new Font("Segoe UI", 14f, FontStyle.Bold))
            using (SolidBrush brushTitulo = new SolidBrush(Color.White))
            {
                StringFormat sf = new StringFormat { Alignment = StringAlignment.Center };
                g.DrawString("Registro de Huella", fontTitulo, brushTitulo, new PointF(this.ClientSize.Width / 2, 82), sf);
            }
        }

        private void CardPanel_Paint(object sender, PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            Rectangle rect = new Rectangle(0, 0, 340 - 1, 350 - 1);
            using (GraphicsPath path = ObtenerRutaRedondeada(rect, 16))
            using (SolidBrush brush = new SolidBrush(ColorCard))
            using (Pen pen = new Pen(ColorBordeInput, 1))
            {
                g.FillPath(brush, path);
                g.DrawPath(pen, path);
            }
        }

        private GraphicsPath ObtenerRutaRedondeada(Rectangle rect, int radio)
        {
            GraphicsPath path = new GraphicsPath();
            int d = radio * 2;
            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        private void CargarEmpleados(IEnumerable<Empleado> empleados)
        {
            if (empleados != null)
            {
                var lista = new List<Empleado>(empleados);
                cbEmpleados.DisplayMember = "NombreCompleto";
                cbEmpleados.ValueMember = "Id";
                cbEmpleados.DataSource = lista;
            }
        }

        private void CargarDedos()
        {
            var listaDedos = new[]
            {
                new { Id = 1, Nombre = "Pulgar Derecho" },
                new { Id = 2, Nombre = "Índice Derecho" },
                new { Id = 3, Nombre = "Medio Derecho" },
                new { Id = 4, Nombre = "Anular Derecho" },
                new { Id = 5, Nombre = "Meñique Derecho" },
                new { Id = 6, Nombre = "Pulgar Izquierdo" },
                new { Id = 7, Nombre = "Índice Izquierdo" },
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

            Constants.ResultCode resOpen = _reader.Open(Constants.CapturePriority.DP_PRIORITY_EXCLUSIVE);
            if (resOpen != Constants.ResultCode.DP_SUCCESS)
            {
                resOpen = _reader.Open(Constants.CapturePriority.DP_PRIORITY_COOPERATIVE);
            }

            if (resOpen == Constants.ResultCode.DP_SUCCESS)
            {
                SolicitarSiguienteMuestra();
            }
        }

        private void SolicitarSiguienteMuestra()
        {
            if (_reader != null)
            {
                _reader.CaptureAsync(
                    Constants.Formats.Fid.ANSI,
                    Constants.CaptureProcessing.DP_IMG_PROC_DEFAULT,
                    _reader.Capabilities.Resolutions[0]);
            }
        }

        private void Reader_OnCaptured(CaptureResult captureResult)
        {
            if (captureResult.ResultCode != Constants.ResultCode.DP_SUCCESS || captureResult.Data == null)
            {
                SolicitarSiguienteMuestra();
                return;
            }

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

            DataResult<Fmd> resultConversion = FeatureExtraction.CreateFmdFromFid(captureResult.Data, Constants.Formats.Fmd.ANSI);

            if (resultConversion.ResultCode == Constants.ResultCode.DP_SUCCESS)
            {
                _enrollmentFmds.Add(resultConversion.Data);

                this.BeginInvoke(new Action(() =>
                {
                    lblInstrucciones.Text = $"Muestra registrada ({_enrollmentFmds.Count} de 4)";
                }));

                if (_enrollmentFmds.Count >= 4)
                {
                    DataResult<Fmd> createResult = Enrollment.CreateEnrollmentFmd(Constants.Formats.Fmd.ANSI, _enrollmentFmds);

                    if (createResult.ResultCode == Constants.ResultCode.DP_SUCCESS)
                    {
                        this.BeginInvoke(new Action(() =>
                        {
                            if (!ValidarSelecciones())
                            {
                                _enrollmentFmds.Clear();
                                lblInstrucciones.Text = "Coloque el dedo en el lector (Muestra 1 de 4)";
                                SolicitarSiguienteMuestra();
                                return;
                            }

                            int empId = Convert.ToInt32(cbEmpleados.SelectedValue);
                            int dedoId = Convert.ToInt32(cbDedos.SelectedValue);
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
                        this.BeginInvoke(new Action(() =>
                        {
                            MessageBox.Show("No se pudo compilar la plantilla. Intente nuevamente.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
                SolicitarSiguienteMuestra();
            }
        }

        private void DetenerCaptura()
        {
            if (_reader != null)
            {
                try
                {
                    _reader.On_Captured -= Reader_OnCaptured;
                    _reader.CancelCapture();
                    _reader.Dispose();
                }
                catch { }
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