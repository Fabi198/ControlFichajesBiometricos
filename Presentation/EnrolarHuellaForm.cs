using DevsFingerPrint.Domain.Models;
using DPUruNet;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

namespace DevsFingerPrint.Presentation
{
    public class EnrolarHuellaForm : Form
    {
        private Reader _reader;
        private List<Fmd> _enrollmentFmds;
        private List<Empleado> _todosEmpleados;
        private HashSet<int> _empleadosConHuellaIds;

        public Huella HuellaCapturada { get; private set; }
        public int EmpleadoIdSeleccionado { get; private set; }
        public int IndiceDedoSeleccionado { get; private set; } = 2; // Default: Índice Derecho (2)

        // Colores
        private readonly Color ColorFondo = Color.FromArgb(10, 15, 29);
        private readonly Color ColorCard = Color.FromArgb(18, 26, 46);
        private readonly Color ColorBordeInput = Color.FromArgb(35, 48, 74);
        private readonly Color ColorAzul = Color.FromArgb(24, 103, 255);
        private readonly Color ColorTextoSub = Color.FromArgb(140, 155, 185);

        // Controles de UI
        private Panel cardPanel;
        private PictureBox pbLogo;
        private CheckBox chkMostrarTodos;
        private ComboBox cbEmpleados;
        private Label lblDedo;
        private PictureBox pbManos;
        private int? dedoHoverId = null;
        private PictureBox pbHuella;
        private Label lblInstrucciones;
        private Button btnSimular;
        private Button btnCancelar;

        // Coordenadas con ajuste fino para centrar exactamente en las yemas
        private readonly Dictionary<int, Rectangle> zonasDedosOriginales = new Dictionary<int, Rectangle>
        {
            // Mano Izquierda (IDs 6 a 10)
            { 6, new Rectangle(135, 310, 60, 45) },   // Pulgar Izquierdo
            { 7, new Rectangle(270, 240, 60, 45) },   // Índice Izquierdo
            { 8, new Rectangle(271, 98, 60, 45) },    // Medio Izquierdo
            { 9, new Rectangle(326, 114, 60, 45) },   // Anular Izquierdo
            { 10, new Rectangle(372, 155, 60, 45) },  // Meñique Izquierdo

            // Mano Derecha (IDs 5 a 1)
            { 5, new Rectangle(492, 155, 60, 45) },   // Meñique Derecho
            { 4, new Rectangle(538, 114, 60, 45) },   // Anular Derecho
            { 3, new Rectangle(591, 98, 60, 45) },    // Medio Derecho
            { 2, new Rectangle(649, 120, 60, 45) },   // Índice Derecho
            { 1, new Rectangle(714, 240, 60, 45) }    // Pulgar Derecho
        };

        public EnrolarHuellaForm(Reader reader, IEnumerable<Empleado> listaEmpleados, IEnumerable<int> empleadosConHuellaIds = null)
        {
            _reader = reader;
            _todosEmpleados = listaEmpleados != null ? new List<Empleado>(listaEmpleados) : new List<Empleado>();
            _empleadosConHuellaIds = empleadosConHuellaIds != null ? new HashSet<int>(empleadosConHuellaIds) : new HashSet<int>();

            ConfigurarVentana();
            CrearControles();
            FiltrarYCargarEmpleados();
            IniciarSecuenciaEnrolamiento();
        }

        private void ConfigurarVentana()
        {
            this.Text = "Enrolamiento Biométrico";
            this.Size = new Size(460, 720);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.BackColor = ColorFondo;
            this.DoubleBuffered = true;
        }

        private void CrearControles()
        {
            pbLogo = new PictureBox
            {
                Size = new Size(48, 48),
                Location = new Point((this.ClientSize.Width - 48) / 2, 12),
                SizeMode = PictureBoxSizeMode.Zoom,
                Image = Properties.Resources.circularColor.ToBitmap(),
                BackColor = Color.Transparent
            };
            this.Controls.Add(pbLogo);

            Label lblTitulo = new Label
            {
                Text = "Registro de Huella",
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 14f, FontStyle.Bold),
                Location = new Point(0, 65),
                Size = new Size(this.ClientSize.Width, 30),
                TextAlign = ContentAlignment.MiddleCenter
            };
            this.Controls.Add(lblTitulo);

            cardPanel = new Panel
            {
                Size = new Size(400, 555),
                Location = new Point((this.ClientSize.Width - 400) / 2, 105),
                BackColor = Color.Transparent
            };
            cardPanel.Paint += CardPanel_Paint;

            chkMostrarTodos = new CheckBox
            {
                Text = "Mostrar empleados que ya tienen huella",
                ForeColor = ColorTextoSub,
                Font = new Font("Segoe UI", 8.5f),
                Location = new Point(20, 15),
                Size = new Size(360, 22),
                Cursor = Cursors.Hand
            };
            chkMostrarTodos.CheckedChanged += (s, e) => FiltrarYCargarEmpleados();

            Label lblEmpleado = new Label
            {
                Text = "Empleado",
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                Location = new Point(20, 42),
                AutoSize = true
            };

            cbEmpleados = new ComboBox
            {
                Location = new Point(20, 64),
                Size = new Size(360, 30),
                BackColor = ColorFondo,
                ForeColor = Color.White,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 9.5f)
            };

            lblDedo = new Label
            {
                Text = "Seleccione el Dedo",
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                Location = new Point(20, 100),
                AutoSize = true
            };

            // PictureBox con la imagen procesada sin fondo negro
            pbManos = new PictureBox
            {
                Location = new Point(20, 122),
                Size = new Size(360, 135),
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = ColorCard, // Del mismo color que la tarjeta para fundirse
                Cursor = Cursors.Hand,
                Image = ObtenerImagenSinFondo(Properties.Resources.manosBlancas)
            };
            pbManos.Paint += PbManos_Paint;
            pbManos.MouseMove += PbManos_MouseMove;
            pbManos.MouseLeave += PbManos_MouseLeave;
            pbManos.MouseClick += PbManos_MouseClick;

            pbHuella = new PictureBox
            {
                Location = new Point(165, 265),
                Size = new Size(70, 70),
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = ColorFondo
            };

            lblInstrucciones = new Label
            {
                Text = "Coloque el dedo en el lector (Muestra 1 de 4)",
                ForeColor = ColorTextoSub,
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                Location = new Point(20, 345),
                Size = new Size(360, 20),
                TextAlign = ContentAlignment.MiddleCenter
            };

            btnSimular = new Button
            {
                Text = "Simular Huella",
                Location = new Point(20, 380),
                Size = new Size(175, 36),
                BackColor = ColorBordeInput,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnSimular.FlatAppearance.BorderSize = 0;
            btnSimular.Click += btnSimularLectura_Click;

            btnCancelar = new Button
            {
                Text = "Cancelar",
                Location = new Point(205, 380),
                Size = new Size(175, 36),
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

            cardPanel.Controls.Add(chkMostrarTodos);
            cardPanel.Controls.Add(lblEmpleado);
            cardPanel.Controls.Add(cbEmpleados);
            cardPanel.Controls.Add(lblDedo);
            cardPanel.Controls.Add(pbManos);
            cardPanel.Controls.Add(pbHuella);
            cardPanel.Controls.Add(lblInstrucciones);
            cardPanel.Controls.Add(btnSimular);
            cardPanel.Controls.Add(btnCancelar);

            this.Controls.Add(cardPanel);

            this.FormClosing += (s, e) => {
                DetenerCaptura();
            };
        }

        private Bitmap ObtenerImagenSinFondo(Bitmap original)
        {
            if (original == null) return null;
            Bitmap bmp = new Bitmap(original);
            // Hace transparente el color negro (o muy cercano a negro) de la imagen original
            bmp.MakeTransparent(Color.Black);
            return bmp;
        }

        private Rectangle ObtenerRectanguloRealImagen()
        {
            if (pbManos.Image == null) return new Rectangle(0, 0, pbManos.Width, pbManos.Height);

            int imgW = pbManos.Image.Width;
            int imgH = pbManos.Image.Height;
            int boxW = pbManos.Width;
            int boxH = pbManos.Height;

            float ratioW = (float)boxW / imgW;
            float ratioH = (float)boxH / imgH;
            float ratio = Math.Min(ratioW, ratioH);

            int realW = (int)(imgW * ratio);
            int realH = (int)(imgH * ratio);
            int left = (boxW - realW) / 2;
            int top = (boxH - realH) / 2;

            return new Rectangle(left, top, realW, realH);
        }

        private Rectangle MapearRectanguloAZonaControl(Rectangle rectOrig, Rectangle rectImgReal)
        {
            if (pbManos.Image == null) return rectOrig;

            float escalaX = (float)rectImgReal.Width / pbManos.Image.Width;
            float escalaY = (float)rectImgReal.Height / pbManos.Image.Height;

            int x = rectImgReal.X + (int)(rectOrig.X * escalaX);
            int y = rectImgReal.Y + (int)(rectOrig.Y * escalaY);
            int w = (int)(rectOrig.Width * escalaX);
            int h = (int)(rectOrig.Height * escalaY);

            return new Rectangle(x, y, w, h);
        }

        private void PbManos_Paint(object sender, PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            Rectangle rectImgReal = ObtenerRectanguloRealImagen();

            using (SolidBrush brushSeleccion = new SolidBrush(Color.FromArgb(150, ColorAzul)))
            using (SolidBrush brushHover = new SolidBrush(Color.FromArgb(80, ColorAzul)))
            using (Pen penBorde = new Pen(Color.White, 1.5f))
            {
                foreach (var kvp in zonasDedosOriginales)
                {
                    int idDedo = kvp.Key;
                    Rectangle rectControl = MapearRectanguloAZonaControl(kvp.Value, rectImgReal);

                    if (idDedo == IndiceDedoSeleccionado)
                    {
                        g.FillEllipse(brushSeleccion, rectControl);
                        g.DrawEllipse(penBorde, rectControl);
                    }
                    else if (idDedo == dedoHoverId)
                    {
                        g.FillEllipse(brushHover, rectControl);
                        g.DrawEllipse(new Pen(Color.FromArgb(200, Color.White), 1f), rectControl);
                    }
                }
            }
        }

        private void PbManos_MouseMove(object sender, MouseEventArgs e)
        {
            Rectangle rectImgReal = ObtenerRectanguloRealImagen();
            int? nuevoHover = null;

            foreach (var kvp in zonasDedosOriginales)
            {
                Rectangle rectControl = MapearRectanguloAZonaControl(kvp.Value, rectImgReal);
                if (rectControl.Contains(e.Location))
                {
                    nuevoHover = kvp.Key;
                    break;
                }
            }

            if (dedoHoverId != nuevoHover)
            {
                dedoHoverId = nuevoHover;
                pbManos.Invalidate();
            }
        }

        private void PbManos_MouseLeave(object sender, EventArgs e)
        {
            if (dedoHoverId != null)
            {
                dedoHoverId = null;
                pbManos.Invalidate();
            }
        }

        private void PbManos_MouseClick(object sender, MouseEventArgs e)
        {
            Rectangle rectImgReal = ObtenerRectanguloRealImagen();

            // Calcular coordenadas relativas a la imagen original basadas en el clic
            if (pbManos.Image != null && rectImgReal.Contains(e.Location))
            {
                float escalaX = (float)pbManos.Image.Width / rectImgReal.Width;
                float escalaY = (float)pbManos.Image.Height / rectImgReal.Height;

                int xOrig = (int)((e.X - rectImgReal.X) * escalaX);
                int yOrig = (int)((e.Y - rectImgReal.Y) * escalaY);

                System.Diagnostics.Debug.WriteLine($"{{ {IndiceDedoSeleccionado}, new Rectangle({xOrig}, {yOrig}, 38, 45) }},");
            }

            // Resto de la lógica de selección...
            foreach (var kvp in zonasDedosOriginales)
            {
                Rectangle rectControl = MapearRectanguloAZonaControl(kvp.Value, rectImgReal);
                if (rectControl.Contains(e.Location))
                {
                    IndiceDedoSeleccionado = kvp.Key;
                    pbManos.Invalidate();
                    break;
                }
            }
        }

        private void FiltrarYCargarEmpleados()
        {
            IEnumerable<Empleado> filtrados = _todosEmpleados;

            if (!chkMostrarTodos.Checked && _empleadosConHuellaIds != null)
            {
                filtrados = _todosEmpleados.Where(e => !_empleadosConHuellaIds.Contains(e.Id));
            }

            var lista = new List<Empleado>(filtrados);
            cbEmpleados.DataSource = null;
            cbEmpleados.DisplayMember = "NombreCompleto";
            cbEmpleados.ValueMember = "Id";
            cbEmpleados.DataSource = lista;
        }

        private void CardPanel_Paint(object sender, PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            Rectangle rect = new Rectangle(0, 0, 400 - 1, 555 - 1);
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

        private bool ValidarSelecciones()
        {
            if (cbEmpleados.SelectedValue == null)
            {
                MessageBox.Show("Por favor, seleccione un empleado válido.", "Atención", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
                            byte[] fmdBytes = createResult.Data.Bytes;

                            if (fmdBytes != null && fmdBytes.Length > 0)
                            {
                                string templateBase64 = Convert.ToBase64String(fmdBytes);

                                EmpleadoIdSeleccionado = empId;

                                HuellaCapturada = new Huella
                                {
                                    EmpleadoId = empId,
                                    IndiceDedo = IndiceDedoSeleccionado,
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
                    string fakeTemplateBase64 = "<FMD><Bytes>AQAAAFQAAABM...</Bytes></FMD>";

                    EmpleadoIdSeleccionado = empId;

                    HuellaCapturada = new Huella
                    {
                        EmpleadoId = empId,
                        IndiceDedo = IndiceDedoSeleccionado,
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