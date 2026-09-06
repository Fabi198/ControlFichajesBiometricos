using DevsFingerPrint.Domain.DTO;
using DevsFingerPrint.Infrastructure.Services;
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace DevsFingerPrint.Presentation
{
    public partial class FormLogin : Form
    {
        private readonly ApiClient _apiClient;

        // Colores basados en la imagen
        private readonly Color ColorFondo = Color.FromArgb(10, 15, 29);       // Oscuro profundo
        private readonly Color ColorCard = Color.FromArgb(18, 26, 46);        // Tarjeta contenedora
        private readonly Color ColorBordeInput = Color.FromArgb(35, 48, 74);  // Borde de los TextBox
        private readonly Color ColorAzul = Color.FromArgb(24, 103, 255);      // Azul vibrante (Botón e Ícono)
        private readonly Color ColorTextoSub = Color.FromArgb(140, 155, 185);  // Gris texto secundario

        // Controles adaptados para Agente
        private TextBox txtClientId;
        private TextBox txtClientSecret;
        private Button btnIngresar;

        public FormLogin(ApiClient apiClient)
        {
            _apiClient = apiClient;

            ConfigurarVentana();
            CrearControles();
        }

        private void ConfigurarVentana()
        {
            this.Text = "Instalación de Puesto Biométrico";
            this.Size = new Size(420, 520);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.BackColor = ColorFondo;
            this.DoubleBuffered = true; // Evita el parpadeo al redibujar
        }

        private void CrearControles()
        {
            // Panel Tarjeta principal
            Panel cardPanel = new Panel
            {
                Size = new Size(340, 300),
                Location = new Point((this.ClientSize.Width - 340) / 2, 160),
                BackColor = Color.Transparent
            };
            cardPanel.Paint += CardPanel_Paint;

            // Label: Client ID
            Label lblClientId = new Label
            {
                Text = "Client ID / Puesto",
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                Location = new Point(25, 25),
                AutoSize = true
            };

            // Input: Client ID
            txtClientId = new TextBox
            {
                Location = new Point(25, 50),
                Size = new Size(290, 30),
                BackColor = ColorFondo,
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Segoe UI", 10.5f)
            };

            // Label: Client Secret
            Label lblClientSecret = new Label
            {
                Text = "Client Secret",
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                Location = new Point(25, 100),
                AutoSize = true
            };

            // Input: Client Secret
            txtClientSecret = new TextBox
            {
                Location = new Point(25, 125),
                Size = new Size(290, 30),
                BackColor = ColorFondo,
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Segoe UI", 10.5f),
                UseSystemPasswordChar = true
            };

            // Botón: Vincular Puesto
            btnIngresar = new Button
            {
                Text = "Vincular Puesto",
                Location = new Point(25, 200),
                Size = new Size(290, 42),
                BackColor = ColorAzul,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnIngresar.FlatAppearance.BorderSize = 0;
            btnIngresar.Click += BtnIngresar_Click;

            cardPanel.Controls.Add(lblClientId);
            cardPanel.Controls.Add(txtClientId);
            cardPanel.Controls.Add(lblClientSecret);
            cardPanel.Controls.Add(txtClientSecret);
            cardPanel.Controls.Add(btnIngresar);

            this.Controls.Add(cardPanel);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            // 1. Dibujar el badge superior "CA" redondeado
            int iconSize = 48;
            int iconX = (this.ClientSize.Width - iconSize) / 2;
            int iconY = 35;
            Rectangle iconRect = new Rectangle(iconX, iconY, iconSize, iconSize);

            using (GraphicsPath path = ObtenerRutaRedondeada(iconRect, 12))
            using (SolidBrush brush = new SolidBrush(ColorAzul))
            {
                g.FillPath(brush, path);
            }

            // Texto dentro del badge "CA"
            using (Font fontBadge = new Font("Segoe UI", 12f, FontStyle.Bold))
            using (SolidBrush textBrush = new SolidBrush(Color.White))
            {
                StringFormat sf = new StringFormat
                {
                    Alignment = StringAlignment.Center,
                    LineAlignment = StringAlignment.Center
                };
                g.DrawString("CA", fontBadge, textBrush, iconRect, sf);
            }

            // 2. Dibujar Titular
            using (Font fontTitulo = new Font("Segoe UI", 15f, FontStyle.Bold))
            using (SolidBrush brushTitulo = new SolidBrush(Color.White))
            {
                StringFormat sf = new StringFormat { Alignment = StringAlignment.Center };
                g.DrawString("Terminal Biométrica", fontTitulo, brushTitulo, new PointF(this.ClientSize.Width / 2, 95), sf);
            }

            // 3. Dibujar Bajada
            using (Font fontSub = new Font("Segoe UI", 9.5f))
            using (SolidBrush brushSub = new SolidBrush(ColorTextoSub))
            {
                StringFormat sf = new StringFormat { Alignment = StringAlignment.Center };
                g.DrawString("Ingrese las credenciales del agente", fontSub, brushSub, new PointF(this.ClientSize.Width / 2, 128), sf);
            }
        }

        private void CardPanel_Paint(object sender, PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            // Dibujar tarjeta con bordes suaves redondeados
            Rectangle rect = new Rectangle(0, 0, 340 - 1, 300 - 1);
            using (GraphicsPath path = ObtenerRutaRedondeada(rect, 16))
            using (SolidBrush brush = new SolidBrush(ColorCard))
            using (Pen pen = new Pen(ColorBordeInput, 1))
            {
                g.FillPath(brush, path);
                g.DrawPath(pen, path);
            }
        }

        private void BtnIngresar_Click(object sender, EventArgs e)
        {
            string clientId = txtClientId.Text.Trim();
            string clientSecret = txtClientSecret.Text.Trim();

            if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret))
            {
                MessageBox.Show("Por favor complete Client ID y Client Secret.", "Atención", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            btnIngresar.Enabled = false;

            // Autenticación como Agente de Sucursal
            if (_apiClient.IniciarSesionAgente(clientId, clientSecret))
            {
                // Guardar credenciales de máquina encriptadas con DPAPI localmente
                CredentialStorage.GuardarCredenciales(clientId, clientSecret);

                MessageBox.Show("Puesto vinculado correctamente.", "Éxito", MessageBoxButtons.OK, MessageBoxIcon.Information);
                this.DialogResult = DialogResult.OK;
                this.Close();
            }
            else
            {
                MessageBox.Show("Credenciales de agente inválidas o servidor no disponible.", "Error de Autenticación", MessageBoxButtons.OK, MessageBoxIcon.Error);
                btnIngresar.Enabled = true;
            }
        }

        // Método auxiliar para trazar rectángulos con esquinas redondeadas
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
    }
}