using DevsFingerPrint.Domain.Models;
using DPUruNet;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

namespace DevsFingerPrint.Presentation
{
    public class EnrolarHuellaForm : Form
    {


        private System.Windows.Forms.Timer timerDuracionGif = new System.Windows.Forms.Timer();
        private Image gifActualEnUso = null;
        private int frameActualContador = 0;
        private int frameLimiteMaximo = 0;










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

        private int? dedoHoverId = null;

        private Panel cardPanel;
        private Button btnCancelar;
        private Label lblInstrucciones;
        private Panel panelHuellaContenedor;
        private PictureBox pbManos;
        private ComboBox cbEmpleados;
        private CheckBox chkMostrarTodos;
        private PictureBox pbLogo;
        private PictureBox pbHuella;
        private Label lblTitulo;
        private PictureBox pbHuellaAnim;
        private int muestraActual = 0;


        // Coordenadas originales para el tamaño estándar de la imagen de manos (360x135)
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
            _empleadosConHuellaIds = empleadosConHuellaIds != null ?
                new HashSet<int>(empleadosConHuellaIds) : new HashSet<int>();

            InitializeComponent();
            lblTitulo.Text = "Enrolamiento de Empleado";
            lblTitulo.ForeColor = Color.White;
            lblTitulo.Font = new Font("Segoe UI", 12, FontStyle.Bold);
            lblTitulo.TextAlign = ContentAlignment.MiddleCenter;
            lblTitulo.AutoSize = true;
            lblTitulo.Location = new Point(
                pbLogo.Left + (pbLogo.Width - lblTitulo.Width) / 2,
                pbLogo.Bottom + 8
            );

            cardPanel.Paint += CardPanel_Paint;
            pbManos.Paint += PbManos_Paint;
            pbManos.MouseMove += PbManos_MouseMove;
            pbManos.MouseLeave += PbManos_MouseLeave;
            pbManos.MouseClick += PbManos_MouseClick;
            pbLogo.Image = Properties.Resources.rectangularColor.ToBitmap();
            pbLogo.SizeMode = PictureBoxSizeMode.Zoom;
            pbLogo.BackColor = Color.Transparent;
            panelHuellaContenedor.BorderStyle = BorderStyle.FixedSingle;
            pbHuella.SizeMode = PictureBoxSizeMode.Zoom;
            lblInstrucciones.ForeColor = Color.White;
            lblInstrucciones.BackColor = Color.Transparent;
            lblInstrucciones.Text = "Muestra registrada (1 de 4)";
            lblInstrucciones.Location = new Point(
                panelHuellaContenedor.Left + (panelHuellaContenedor.Width - lblInstrucciones.Width) / 2,
                panelHuellaContenedor.Bottom + 10
            );

            pbManos.Image = ObtenerImagenSinFondo(Properties.Resources.manosBlancas);
            pbManos.SizeMode = PictureBoxSizeMode.Zoom;
            pbManos.BackColor = ColorCard;
            pbManos.Cursor = Cursors.Hand;
            cbEmpleados.DropDownStyle = ComboBoxStyle.DropDownList;
            chkMostrarTodos.ForeColor = Color.White;
            chkMostrarTodos.BackColor = Color.Transparent;
            chkMostrarTodos.CheckedChanged += (s, e) => FiltrarYCargarEmpleados();

            // Configuración inicial del PictureBox del GIF animado de huella
            pbHuellaAnim.SizeMode = PictureBoxSizeMode.CenterImage;
            pbHuellaAnim.BackColor = ColorCard;

            // Iniciar con la primera parte del GIF o limpio (aquí puedes asignar huella01 si prefieres que arranque lleno el paso 1)
            ActualizarProgresoHuella(0);

            btnCancelar.Text = "Cancelar";
            btnCancelar.BackColor = ColorBordeInput;
            btnCancelar.ForeColor = Color.White;
            btnCancelar.FlatStyle = FlatStyle.Flat;
            btnCancelar.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
            btnCancelar.Cursor = Cursors.Hand;
            btnCancelar.FlatAppearance.BorderSize = 0;
            btnCancelar.Click += (s, e) => {
                DetenerCaptura();
                this.DialogResult = DialogResult.Cancel;
            };

            panelHuellaContenedor.BorderStyle = BorderStyle.None;
            pbHuella.Location = new Point(
                (panelHuellaContenedor.Width - pbHuella.Width) / 2,
                (panelHuellaContenedor.Height - pbHuella.Height) / 2
            );

            ConfigurarVentana();
            FiltrarYCargarEmpleados();
            IniciarSecuenciaEnrolamiento();
        }

        private void ConfigurarVentana()
        {
            this.Text = "Enrolamiento Biométrico";
            this.Size = new Size(540, 660);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.BackColor = ColorFondo;
            this.DoubleBuffered = true;
        }

        private void PanelHuella_Paint(object sender, PaintEventArgs e)
        {
            using (Pen pen = new Pen(Color.FromArgb(80, 140, 255), 2))
            {
                e.Graphics.DrawRectangle(pen, 0, 0, panelHuellaContenedor.Width - 1, panelHuellaContenedor.Height - 1);
            }
        }

        private Bitmap ObtenerImagenSinFondo(Bitmap original)
        {
            if (original == null) return null;
            Bitmap bmp = new Bitmap(original);
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

            Rectangle rect = new Rectangle(0, 0, cardPanel.Width - 1, cardPanel.Height - 1);
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
                    muestraActual++;
                    ActualizarProgresoHuella(muestraActual);
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
                                muestraActual = 0;
                                ActualizarProgresoHuella(muestraActual);
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
                                muestraActual = 0;
                                ActualizarProgresoHuella(muestraActual);
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
                            muestraActual = 0;
                            ActualizarProgresoHuella(muestraActual);
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

        private void InitializeComponent()
        {
            this.cardPanel = new System.Windows.Forms.Panel();
            this.lblTitulo = new System.Windows.Forms.Label();
            this.btnCancelar = new System.Windows.Forms.Button();
            this.lblInstrucciones = new System.Windows.Forms.Label();
            this.panelHuellaContenedor = new System.Windows.Forms.Panel();
            this.pbHuella = new System.Windows.Forms.PictureBox();
            this.pbManos = new System.Windows.Forms.PictureBox();
            this.cbEmpleados = new System.Windows.Forms.ComboBox();
            this.chkMostrarTodos = new System.Windows.Forms.CheckBox();
            this.pbLogo = new System.Windows.Forms.PictureBox();
            this.pbHuellaAnim = new System.Windows.Forms.PictureBox();
            this.cardPanel.SuspendLayout();
            this.panelHuellaContenedor.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.pbHuella)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.pbManos)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.pbLogo)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.pbHuellaAnim)).BeginInit();
            this.SuspendLayout();
            // 
            // cardPanel
            // 
            this.cardPanel.Controls.Add(this.pbHuellaAnim);
            this.cardPanel.Controls.Add(this.lblTitulo);
            this.cardPanel.Controls.Add(this.btnCancelar);
            this.cardPanel.Controls.Add(this.lblInstrucciones);
            this.cardPanel.Controls.Add(this.panelHuellaContenedor);
            this.cardPanel.Controls.Add(this.pbManos);
            this.cardPanel.Controls.Add(this.cbEmpleados);
            this.cardPanel.Controls.Add(this.chkMostrarTodos);
            this.cardPanel.Controls.Add(this.pbLogo);
            this.cardPanel.Location = new System.Drawing.Point(12, 12);
            this.cardPanel.Name = "cardPanel";
            this.cardPanel.Size = new System.Drawing.Size(460, 601);
            this.cardPanel.TabIndex = 0;
            // 
            // lblTitulo
            // 
            this.lblTitulo.AutoSize = true;
            this.lblTitulo.BackColor = System.Drawing.Color.Transparent;
            this.lblTitulo.Location = new System.Drawing.Point(159, 73);
            this.lblTitulo.Name = "lblTitulo";
            this.lblTitulo.Size = new System.Drawing.Size(133, 13);
            this.lblTitulo.TabIndex = 7;
            this.lblTitulo.Text = "Enrolamiento de Empleado";
            this.lblTitulo.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // btnCancelar
            // 
            this.btnCancelar.Location = new System.Drawing.Point(333, 557);
            this.btnCancelar.Name = "btnCancelar";
            this.btnCancelar.Size = new System.Drawing.Size(75, 23);
            this.btnCancelar.TabIndex = 6;
            this.btnCancelar.Text = "Cancelar";
            this.btnCancelar.UseVisualStyleBackColor = true;
            // 
            // lblInstrucciones
            // 
            this.lblInstrucciones.AutoSize = true;
            this.lblInstrucciones.Location = new System.Drawing.Point(167, 519);
            this.lblInstrucciones.Name = "lblInstrucciones";
            this.lblInstrucciones.Size = new System.Drawing.Size(35, 13);
            this.lblInstrucciones.TabIndex = 5;
            this.lblInstrucciones.Text = "label1";
            // 
            // panelHuellaContenedor
            // 
            this.panelHuellaContenedor.Controls.Add(this.pbHuella);
            this.panelHuellaContenedor.Location = new System.Drawing.Point(191, 411);
            this.panelHuellaContenedor.Name = "panelHuellaContenedor";
            this.panelHuellaContenedor.Size = new System.Drawing.Size(76, 100);
            this.panelHuellaContenedor.TabIndex = 4;
            // 
            // pbHuella
            // 
            this.pbHuella.Location = new System.Drawing.Point(3, 3);
            this.pbHuella.Name = "pbHuella";
            this.pbHuella.Size = new System.Drawing.Size(70, 94);
            this.pbHuella.TabIndex = 0;
            this.pbHuella.TabStop = false;
            // 
            // pbManos
            // 
            this.pbManos.Location = new System.Drawing.Point(40, 163);
            this.pbManos.Name = "pbManos";
            this.pbManos.Size = new System.Drawing.Size(381, 242);
            this.pbManos.TabIndex = 3;
            this.pbManos.TabStop = false;
            // 
            // cbEmpleados
            // 
            this.cbEmpleados.FormattingEnabled = true;
            this.cbEmpleados.Location = new System.Drawing.Point(82, 133);
            this.cbEmpleados.Name = "cbEmpleados";
            this.cbEmpleados.Size = new System.Drawing.Size(306, 21);
            this.cbEmpleados.TabIndex = 2;
            // 
            // chkMostrarTodos
            // 
            this.chkMostrarTodos.AutoSize = true;
            this.chkMostrarTodos.Location = new System.Drawing.Point(83, 110);
            this.chkMostrarTodos.Name = "chkMostrarTodos";
            this.chkMostrarTodos.Size = new System.Drawing.Size(167, 17);
            this.chkMostrarTodos.TabIndex = 1;
            this.chkMostrarTodos.Text = "Mostrar empleados con huella";
            this.chkMostrarTodos.UseVisualStyleBackColor = true;
            // 
            // pbLogo
            // 
            this.pbLogo.Location = new System.Drawing.Point(162, 15);
            this.pbLogo.Name = "pbLogo";
            this.pbLogo.Size = new System.Drawing.Size(130, 50);
            this.pbLogo.TabIndex = 0;
            this.pbLogo.TabStop = false;
            // 
            // pbHuellaAnim
            // 
            this.pbHuellaAnim.Location = new System.Drawing.Point(40, 412);
            this.pbHuellaAnim.Name = "pbHuellaAnim";
            this.pbHuellaAnim.Size = new System.Drawing.Size(127, 168);
            this.pbHuellaAnim.TabIndex = 8;
            this.pbHuellaAnim.TabStop = false;
            // 
            // EnrolarHuellaForm
            // 
            this.ClientSize = new System.Drawing.Size(599, 717);
            this.Controls.Add(this.cardPanel);
            this.Name = "EnrolarHuellaForm";
            this.Load += new System.EventHandler(this.EnrolarHuellaForm_Load);
            this.cardPanel.ResumeLayout(false);
            this.cardPanel.PerformLayout();
            this.panelHuellaContenedor.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.pbHuella)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.pbManos)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.pbLogo)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.pbHuellaAnim)).EndInit();
            this.ResumeLayout(false);
        }

        private void EnrolarHuellaForm_Load(object sender, EventArgs e)
        {
            cardPanel.Left = (this.ClientSize.Width - cardPanel.Width) / 2;
            muestraActual = 0;
            pbHuella.Image = null;

            // En el constructor o en EnrolarHuellaForm_Load:
            timerDuracionGif.Tick += (s, ev) =>
            {
                timerDuracionGif.Stop();

                // Cuando se cumple el tiempo exacto del GIF, detenemos la animación 
                // y lo dejamos congelado en su último fotograma de forma forzada.
                if (gifActualEnUso != null)
                {
                    try
                    {
                        ImageAnimator.StopAnimate(gifActualEnUso, OnFrameChanged);

                        // Forzamos el último fotograma disponible
                        Guid idDimension = FrameDimension.Time.Guid;
                        int totalFrames = gifActualEnUso.GetFrameCount(new FrameDimension(idDimension));
                        gifActualEnUso.SelectActiveFrame(new FrameDimension(idDimension), totalFrames - 1);

                        pbHuellaAnim.Invalidate();
                    }
                    catch { }
                }
            };
        }

        private void ActualizarProgresoHuella(int muestra)
        {
            // 1. Detener animación previa
            if (gifActualEnUso != null)
            {
                try { ImageAnimator.StopAnimate(gifActualEnUso, OnFrameChanged); } catch { }
            }

            // 2. Asignar recurso y configurar cuántos fotogramas exactos debe reproducir este tramo
            switch (muestra)
            {
                case 1:
                    gifActualEnUso = Properties.Resources.huella01;
                    frameLimiteMaximo = 9;  // Del 01 al 09
                    break;
                case 2:
                    gifActualEnUso = Properties.Resources.huella02;
                    frameLimiteMaximo = 3;  // Del 09 al 12 (3 saltos de fotograma)
                    break;
                case 3:
                    gifActualEnUso = Properties.Resources.huella03;
                    frameLimiteMaximo = 5;  // Del 12 al 17 (5 saltos de fotograma)
                    break;
                case 4:
                    gifActualEnUso = Properties.Resources.huella04;
                    frameLimiteMaximo = 44; // Del 17 al 61 (44 saltos de fotograma)
                    break;
                default:
                    pbHuellaAnim.Image = null;
                    gifActualEnUso = null;
                    return;
            }

            frameActualContador = 0;
            pbHuellaAnim.Image = gifActualEnUso;

            // 3. Arrancar la animación por fotogramas controlados
            if (gifActualEnUso != null && ImageAnimator.CanAnimate(gifActualEnUso))
            {
                ImageAnimator.Animate(gifActualEnUso, OnFrameChanged);
            }
        }

        private void OnFrameChanged(object sender, EventArgs e)
        {
            if (pbHuellaAnim.InvokeRequired)
            {
                pbHuellaAnim.BeginInvoke(new EventHandler(OnFrameChanged), sender, e);
                return;
            }

            // Avanzamos al siguiente fotograma físico del GIF
            ImageAnimator.UpdateFrames(gifActualEnUso);
            frameActualContador++;

            // Si alcanzamos exactamente el límite de fotogramas de este tramo:
            if (frameActualContador >= frameLimiteMaximo)
            {
                // Detenemos el motor de animación de inmediato
                ImageAnimator.StopAnimate(gifActualEnUso, OnFrameChanged);

                try
                {
                    // Forzamos al objeto a posicionarse en su fotograma final absoluto
                    FrameDimension dimension = new FrameDimension(gifActualEnUso.FrameDimensionsList[0]);
                    int totalFramesTotales = gifActualEnUso.GetFrameCount(dimension);
                    gifActualEnUso.SelectActiveFrame(dimension, totalFramesTotales - 1);
                }
                catch { }
            }

            pbHuellaAnim.Invalidate();
        }
    }
}