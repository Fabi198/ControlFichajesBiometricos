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
        private List<Huella> _todasLasHuellasLocales;
        private HashSet<int> _empleadosConHuellaIds;
        private HashSet<int> _dedosRegistradosDelEmpleadoActual = new HashSet<int>();

        public Huella HuellaCapturada { get; private set; }
        public int EmpleadoIdSeleccionado { get; private set; }
        public int IndiceDedoSeleccionado { get; private set; } = 1;

        // Colores
        private readonly Color ColorFondo = Color.FromArgb(10, 15, 29);
        private readonly Color ColorCard = Color.FromArgb(18, 26, 46);
        private readonly Color ColorBordeInput = Color.FromArgb(35, 48, 74);
        private readonly Color ColorAzul = Color.FromArgb(24, 103, 255);

        private int? dedoHoverId = null;

        private Panel cardPanel;
        private Button btnCancelar;
        private Button btnSimularMuestra; // Botón físico/visual para simular las 4 muestras en modo mock
        private PictureBox pbManos;
        private ComboBox cbEmpleados;
        private PictureBox pbLogo;
        private Label lblTitulo;
        private PictureBox pbHuellaAnim;
        private int muestraActual = 0;
        private Label lblInstrucciones;
        private Label lblFingerSelect;
        private GroupBox gbEmpleados;
        private RadioButton rbConHuella;
        private RadioButton rbSinHuella;

        private readonly Dictionary<int, Rectangle> zonasDedosOriginales = new Dictionary<int, Rectangle>
        {
            { 6, new Rectangle(135, 310, 60, 60) },
            { 7, new Rectangle(235, 210, 60, 60) },
            { 8, new Rectangle(300, 180, 60, 60) },
            { 9, new Rectangle(360, 195, 60, 60) },
            { 10, new Rectangle(420, 255, 60, 60) },
            { 5, new Rectangle(495, 255, 60, 60) },
            { 4, new Rectangle(565, 195, 60, 60) },
            { 3, new Rectangle(620, 180, 60, 60) },
            { 2, new Rectangle(685, 210, 60, 60) },
            { 1, new Rectangle(785, 310, 60, 60) }
        };

        public EnrolarHuellaForm(Reader reader, IEnumerable<Empleado> listaEmpleados, IEnumerable<Huella> huellasLocales = null)
        {
            _reader = reader;
            _todosEmpleados = listaEmpleados != null ? new List<Empleado>(listaEmpleados) : new List<Empleado>();
            _todasLasHuellasLocales = huellasLocales != null ? new List<Huella>(huellasLocales) : new List<Huella>();

            _empleadosConHuellaIds = new HashSet<int>(_todasLasHuellasLocales.Select(h => h.EmpleadoId));

            InitializeComponent();
            lblTitulo.Text = "Enrolamiento de Empleado (Mock)";
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

            lblFingerSelect.ForeColor = Color.White;
            lblFingerSelect.BackColor = Color.Transparent;

            gbEmpleados.ForeColor = Color.White;
            gbEmpleados.BackColor = Color.Transparent;

            lblInstrucciones.ForeColor = Color.White;
            lblInstrucciones.BackColor = Color.Transparent;
            lblInstrucciones.Text = _reader == null ? "Modo Mock: Haga clic en 'Simular Captura'" : "Apoye el dedo en el lector";
            lblInstrucciones.TextAlign = ContentAlignment.MiddleCenter;
            lblInstrucciones.Location = new Point(
                pbHuellaAnim.Left + (pbHuellaAnim.Width - lblInstrucciones.Width) / 2,
                pbHuellaAnim.Bottom + 10
            );

            pbManos.Image = ObtenerImagenSinFondo(Properties.Resources.manosBlancas);
            pbManos.SizeMode = PictureBoxSizeMode.Zoom;
            pbManos.BackColor = ColorCard;
            pbManos.Cursor = Cursors.Hand;
            cbEmpleados.DropDownStyle = ComboBoxStyle.DropDownList;

            cbEmpleados.SelectedIndexChanged += CbEmpleados_SelectedIndexChanged;

            pbHuellaAnim.SizeMode = PictureBoxSizeMode.CenterImage;
            pbHuellaAnim.BackColor = ColorCard;

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

            // Configurar el botón de simulación si estamos sin hardware real
            if (_reader == null)
            {
                ConfigurarBotonSimulacion();
            }

            ConfigurarVentana();
            FiltrarYCargarEmpleados();
            IniciarSecuenciaEnrolamiento();
        }

        private void ConfigurarBotonSimulacion()
        {
            btnSimularMuestra = new Button();
            btnSimularMuestra.Text = "Simular Captura Dedo";
            btnSimularMuestra.BackColor = ColorAzul;
            btnSimularMuestra.ForeColor = Color.White;
            btnSimularMuestra.FlatStyle = FlatStyle.Flat;
            btnSimularMuestra.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
            btnSimularMuestra.Cursor = Cursors.Hand;
            btnSimularMuestra.FlatAppearance.BorderSize = 0;
            // Ubicamos el botón de simulación en el panel debajo de las instrucciones
            btnSimularMuestra.Size = new Size(160, 30);
            btnSimularMuestra.Location = new Point((cardPanel.Width - btnSimularMuestra.Width) / 2, 536);
            btnSimularMuestra.Click += BtnSimularMuestra_Click;

            cardPanel.Controls.Add(btnSimularMuestra);
            btnCancelar.Location = new Point(btnSimularMuestra.Right + 10, 536);
        }

        private void BtnSimularMuestra_Click(object sender, EventArgs e)
        {
            // Simulamos el flujo de llegada de una captura parcial
            muestraActual++;
            if (muestraActual > 4) muestraActual = 1;

            if (muestraActual < 4)
            {
                System.Media.SystemSounds.Hand.Play();
                ActualizarProgresoHuella(muestraActual);
                lblInstrucciones.Text = $"Muestra simulada ({muestraActual} de 4)";
            }
            else
            {
                // Cuarta y última muestra: genera template falso y finaliza con éxito
                ActualizarProgresoHuella(4);
                lblInstrucciones.Text = "¡Huella registrada con éxito (Mock)!";

                if (!ValidarSelecciones()) return;

                int empId = Convert.ToInt32(cbEmpleados.SelectedValue);
                byte[] dummyBytes = new byte[200];
                new Random().NextBytes(dummyBytes);
                string templateBase64 = Convert.ToBase64String(dummyBytes);

                EmpleadoIdSeleccionado = empId;
                HuellaCapturada = new Huella
                {
                    EmpleadoId = empId,
                    IndiceDedo = IndiceDedoSeleccionado,
                    TemplateBiometrico = templateBase64
                };

                MessageBox.Show("¡Huella enrolada exitosamente (Mock)!", "Éxito", MessageBoxButtons.OK, MessageBoxIcon.Information);
                this.DialogResult = DialogResult.OK;
                this.Close();
            }
        }

        private void ConfigurarVentana()
        {
            this.Text = "Enrolamiento Biométrico (Mock)";
            this.Size = new Size(540, 660);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.BackColor = ColorFondo;
            this.DoubleBuffered = true;
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

        private void CbEmpleados_SelectedIndexChanged(object sender, EventArgs e)
        {
            _dedosRegistradosDelEmpleadoActual.Clear();

            if (cbEmpleados.SelectedValue is int empleadoId)
            {
                var huellasDelEmpleado = _todasLasHuellasLocales.Where(h => h.EmpleadoId == empleadoId);
                foreach (var h in huellasDelEmpleado)
                {
                    _dedosRegistradosDelEmpleadoActual.Add(h.IndiceDedo);
                }

                if (_dedosRegistradosDelEmpleadoActual.Contains(IndiceDedoSeleccionado))
                {
                    for (int i = 1; i <= 10; i++)
                    {
                        if (!_dedosRegistradosDelEmpleadoActual.Contains(i))
                        {
                            IndiceDedoSeleccionado = i;
                            break;
                        }
                    }
                }
            }

            pbManos.Invalidate();
        }

        private void PbManos_Paint(object sender, PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            Rectangle rectImgReal = ObtenerRectanguloRealImagen();

            using (SolidBrush brushSeleccion = new SolidBrush(Color.FromArgb(150, ColorAzul)))
            using (SolidBrush brushHover = new SolidBrush(Color.FromArgb(80, ColorAzul)))
            using (SolidBrush brushOcupado = new SolidBrush(Color.FromArgb(120, 80, 80, 80)))
            using (Pen penBorde = new Pen(Color.White, 1.5f))
            using (Pen penOcupado = new Pen(Color.FromArgb(140, 140, 140), 1f))
            {
                foreach (var kvp in zonasDedosOriginales)
                {
                    int idDedo = kvp.Key;
                    Rectangle rectControl = MapearRectanguloAZonaControl(kvp.Value, rectImgReal);

                    if (_dedosRegistradosDelEmpleadoActual.Contains(idDedo))
                    {
                        g.FillEllipse(brushOcupado, rectControl);
                        g.DrawEllipse(penOcupado, rectControl);
                    }
                    else if (idDedo == IndiceDedoSeleccionado)
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
                    int idDedo = kvp.Key;
                    if (!_dedosRegistradosDelEmpleadoActual.Contains(idDedo))
                    {
                        nuevoHover = idDedo;
                    }
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
                    int idDedo = kvp.Key;

                    if (_dedosRegistradosDelEmpleadoActual.Contains(idDedo))
                    {
                        MessageBox.Show("Este dedo ya tiene una huella registrada para el empleado seleccionado.", "Dedo Ocupado", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }

                    IndiceDedoSeleccionado = idDedo;
                    pbManos.Invalidate();
                    break;
                }
            }
        }

        private void FiltrarYCargarEmpleados()
        {
            if (_todosEmpleados == null) return;

            IEnumerable<Empleado> filtrados;

            if (rbConHuella != null && rbConHuella.Checked)
            {
                filtrados = _todosEmpleados.Where(e => _empleadosConHuellaIds != null && _empleadosConHuellaIds.Contains(e.Id));
            }
            else
            {
                filtrados = _todosEmpleados.Where(e => _empleadosConHuellaIds == null || !_empleadosConHuellaIds.Contains(e.Id));
            }

            var lista = new List<Empleado>(filtrados);
            cbEmpleados.DataSource = null;
            cbEmpleados.DisplayMember = "NombreCompleto";
            cbEmpleados.ValueMember = "Id";
            cbEmpleados.DataSource = lista;
        }

        private void RbFiltro_CheckedChanged(object sender, EventArgs e)
        {
            if (sender is RadioButton rb && rb.Checked)
            {
                FiltrarYCargarEmpleados();
            }
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
            if (_reader == null) return; // Si es null (mock), no intenta abrir hardware real

            _enrollmentFmds = new List<Fmd>();
            _reader.On_Captured += Reader_OnCaptured;

            Constants.ResultCode resOpen = _reader.Open(Constants.CapturePriority.DP_PRIORITY_EXCLUSIVE);
            if (resOpen != Constants.ResultCode.DP_SUCCESS)
            {
                _reader.Open(Constants.CapturePriority.DP_PRIORITY_COOPERATIVE);
            }
        }

        private void Reader_OnCaptured(CaptureResult captureResult)
        {
            // Lógica original de hardware si llegase a usarse
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

        private void InitializeComponent()
        {
            this.cardPanel = new System.Windows.Forms.Panel();
            this.gbEmpleados = new System.Windows.Forms.GroupBox();
            this.rbConHuella = new System.Windows.Forms.RadioButton();
            this.rbSinHuella = new System.Windows.Forms.RadioButton();
            this.cbEmpleados = new System.Windows.Forms.ComboBox();
            this.lblFingerSelect = new System.Windows.Forms.Label();
            this.lblInstrucciones = new System.Windows.Forms.Label();
            this.pbHuellaAnim = new System.Windows.Forms.PictureBox();
            this.lblTitulo = new System.Windows.Forms.Label();
            this.btnCancelar = new System.Windows.Forms.Button();
            this.pbManos = new System.Windows.Forms.PictureBox();
            this.pbLogo = new System.Windows.Forms.PictureBox();
            this.cardPanel.SuspendLayout();
            this.gbEmpleados.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.pbHuellaAnim)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.pbManos)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.pbLogo)).BeginInit();
            this.SuspendLayout();

            // cardPanel
            this.cardPanel.Controls.Add(this.gbEmpleados);
            this.cardPanel.Controls.Add(this.lblFingerSelect);
            this.cardPanel.Controls.Add(this.lblInstrucciones);
            this.cardPanel.Controls.Add(this.pbHuellaAnim);
            this.cardPanel.Controls.Add(this.lblTitulo);
            this.cardPanel.Controls.Add(this.btnCancelar);
            this.cardPanel.Controls.Add(this.pbManos);
            this.cardPanel.Controls.Add(this.pbLogo);
            this.cardPanel.Location = new System.Drawing.Point(12, 12);
            this.cardPanel.Name = "cardPanel";
            this.cardPanel.Size = new System.Drawing.Size(460, 579);
            this.cardPanel.TabIndex = 0;

            // gbEmpleados
            this.gbEmpleados.Controls.Add(this.rbConHuella);
            this.gbEmpleados.Controls.Add(this.rbSinHuella);
            this.gbEmpleados.Controls.Add(this.cbEmpleados);
            this.gbEmpleados.Location = new System.Drawing.Point(86, 93);
            this.gbEmpleados.Name = "gbEmpleados";
            this.gbEmpleados.Size = new System.Drawing.Size(295, 93);
            this.gbEmpleados.TabIndex = 10;
            this.gbEmpleados.TabStop = false;
            this.gbEmpleados.Text = "Listar empleados:";

            // rbConHuella
            this.rbConHuella.AutoSize = true;
            this.rbConHuella.Location = new System.Drawing.Point(7, 41);
            this.rbConHuella.Name = "rbConHuella";
            this.rbConHuella.Size = new System.Drawing.Size(133, 17);
            this.rbConHuella.TabIndex = 1;
            this.rbConHuella.TabStop = true;
            this.rbConHuella.Text = "Con huellas en sistema";
            this.rbConHuella.UseVisualStyleBackColor = true;
            this.rbConHuella.CheckedChanged += new System.EventHandler(this.RbFiltro_CheckedChanged);

            // rbSinHuella
            this.rbSinHuella.AutoSize = true;
            this.rbSinHuella.Checked = true;
            this.rbSinHuella.Location = new System.Drawing.Point(7, 20);
            this.rbSinHuella.Name = "rbSinHuella";
            this.rbSinHuella.Size = new System.Drawing.Size(129, 17);
            this.rbSinHuella.TabIndex = 0;
            this.rbSinHuella.TabStop = true;
            this.rbSinHuella.Text = "Sin huellas en sistema";
            this.rbSinHuella.UseVisualStyleBackColor = true;
            this.rbSinHuella.CheckedChanged += new System.EventHandler(this.RbFiltro_CheckedChanged);

            // cbEmpleados
            this.cbEmpleados.FormattingEnabled = true;
            this.cbEmpleados.Location = new System.Drawing.Point(12, 64);
            this.cbEmpleados.Name = "cbEmpleados";
            this.cbEmpleados.Size = new System.Drawing.Size(270, 21);
            this.cbEmpleados.TabIndex = 2;

            // lblFingerSelect
            this.lblFingerSelect.AutoSize = true;
            this.lblFingerSelect.Location = new System.Drawing.Point(156, 192);
            this.lblFingerSelect.Name = "lblFingerSelect";
            this.lblFingerSelect.Size = new System.Drawing.Size(142, 13);
            this.lblFingerSelect.TabIndex = 9;
            this.lblFingerSelect.Text = "Seleccione el dedo a enrolar";

            // lblInstrucciones
            this.lblInstrucciones.AutoSize = true;
            this.lblInstrucciones.Location = new System.Drawing.Point(215, 500);
            this.lblInstrucciones.Name = "lblInstrucciones";
            this.lblInstrucciones.Size = new System.Drawing.Size(35, 13);
            this.lblInstrucciones.TabIndex = 5;
            this.lblInstrucciones.Text = "label1";

            // pbHuellaAnim
            this.pbHuellaAnim.Location = new System.Drawing.Point(177, 385);
            this.pbHuellaAnim.Name = "pbHuellaAnim";
            this.pbHuellaAnim.Size = new System.Drawing.Size(109, 112);
            this.pbHuellaAnim.TabIndex = 8;
            this.pbHuellaAnim.TabStop = false;

            // lblTitulo
            this.lblTitulo.AutoSize = true;
            this.lblTitulo.BackColor = System.Drawing.Color.Transparent;
            this.lblTitulo.Location = new System.Drawing.Point(160, 67);
            this.lblTitulo.Name = "lblTitulo";
            this.lblTitulo.Size = new System.Drawing.Size(133, 13);
            this.lblTitulo.TabIndex = 7;
            this.lblTitulo.Text = "Enrolamiento de Empleado";
            this.lblTitulo.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;

            // btnCancelar
            this.btnCancelar.Location = new System.Drawing.Point(354, 536);
            this.btnCancelar.Name = "btnCancelar";
            this.btnCancelar.Size = new System.Drawing.Size(75, 23);
            this.btnCancelar.TabIndex = 6;
            this.btnCancelar.Text = "Cancelar";
            this.btnCancelar.UseVisualStyleBackColor = true;

            // pbManos
            this.pbManos.Location = new System.Drawing.Point(86, 209);
            this.pbManos.Name = "pbManos";
            this.pbManos.Size = new System.Drawing.Size(295, 168);
            this.pbManos.TabIndex = 3;
            this.pbManos.TabStop = false;

            // pbLogo
            this.pbLogo.Location = new System.Drawing.Point(162, 7);
            this.pbLogo.Name = "pbLogo";
            this.pbLogo.Size = new System.Drawing.Size(130, 50);
            this.pbLogo.TabIndex = 0;
            this.pbLogo.TabStop = false;

            // EnrolarHuellaForm
            this.ClientSize = new System.Drawing.Size(599, 717);
            this.Controls.Add(this.cardPanel);
            this.Name = "EnrolarHuellaForm";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.EnrolarHuellaForm_FormClosing);
            this.Load += new System.EventHandler(this.EnrolarHuellaForm_Load);
            this.cardPanel.ResumeLayout(false);
            this.cardPanel.PerformLayout();
            this.gbEmpleados.ResumeLayout(false);
            this.gbEmpleados.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.pbHuellaAnim)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.pbManos)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.pbLogo)).EndInit();
            this.ResumeLayout(false);
        }

        private void EnrolarHuellaForm_Load(object sender, EventArgs e)
        {
            cardPanel.Left = (this.ClientSize.Width - cardPanel.Width) / 2;
            muestraActual = 0;

            timerDuracionGif.Tick += (s, ev) =>
            {
                timerDuracionGif.Stop();

                if (gifActualEnUso != null)
                {
                    try
                    {
                        ImageAnimator.StopAnimate(gifActualEnUso, OnFrameChanged);

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
            if (gifActualEnUso != null)
            {
                try { ImageAnimator.StopAnimate(gifActualEnUso, OnFrameChanged); } catch { }
            }

            switch (muestra)
            {
                case 1:
                    gifActualEnUso = Properties.Resources.huella01;
                    frameLimiteMaximo = 9;
                    break;
                case 2:
                    gifActualEnUso = Properties.Resources.huella02;
                    frameLimiteMaximo = 3;
                    break;
                case 3:
                    gifActualEnUso = Properties.Resources.huella03;
                    frameLimiteMaximo = 5;
                    break;
                case 4:
                    gifActualEnUso = Properties.Resources.huella04;
                    frameLimiteMaximo = 145;
                    break;
                default:
                    pbHuellaAnim.Image = null;
                    gifActualEnUso = null;
                    return;
            }

            if (gifActualEnUso != null)
            {
                try
                {
                    FrameDimension dimension = new FrameDimension(gifActualEnUso.FrameDimensionsList[0]);
                    gifActualEnUso.SelectActiveFrame(dimension, 0);
                }
                catch { }
            }

            frameActualContador = 0;
            pbHuellaAnim.SizeMode = PictureBoxSizeMode.StretchImage;
            pbHuellaAnim.Image = gifActualEnUso;

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

            ImageAnimator.UpdateFrames(gifActualEnUso);
            frameActualContador++;

            if (frameActualContador >= frameLimiteMaximo)
            {
                ImageAnimator.StopAnimate(gifActualEnUso, OnFrameChanged);
            }

            pbHuellaAnim.Invalidate();
        }

        private void EnrolarHuellaForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (this.DialogResult == DialogResult.OK)
            {
                DetenerCaptura();
                return;
            }

            if (e.CloseReason == CloseReason.UserClosing)
            {
                DialogResult resultado = MessageBox.Show(
                    "¿Está seguro que desea cancelar el enrolamiento?",
                    "Confirmación",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);

                if (resultado == DialogResult.No)
                {
                    e.Cancel = true;
                    return;
                }
            }

            DetenerCaptura();
            this.DialogResult = DialogResult.Cancel;
        }
    }
}