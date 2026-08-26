using DevsFingerPrint.Domain.Models;
using DPUruNet;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace DevsFingerPrint.Presentation
{
    public class EnrolarHuellaForm : Form
    {


        private Reader _reader;
        private List<Fmd> _enrollmentFmds;
        private int _empleadoId;


        public Huella HuellaCapturada { get; private set; }

        private Label lblInstrucciones;
        private PictureBox pbHuella;
        private Button btnCancelar;


        public EnrolarHuellaForm(Reader reader, int empleadoId)
        {
            _reader = reader;
            _empleadoId = empleadoId;

            InicializarComponentes();
            IniciarSecuenciaEnrolamiento();
        }

        private void InicializarComponentes()
        {
            this.Text = "Enrolar Nueva Huella - U.are.U 4500";
            this.Size = new Size(350, 400);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;

            lblInstrucciones = new Label
            {
                Text = "Coloque el dedo en el lector (Muestra 1 de 4)",
                Dock = DockStyle.Top,
                Height = 40,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 10, FontStyle.Bold)
            };

            pbHuella = new PictureBox
            {
                Dock = DockStyle.Fill,
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.Black
            };

            btnCancelar = new Button
            {
                Text = "Cancelar",
                Dock = DockStyle.Bottom,
                Height = 40
            };
            btnCancelar.Click += (s, e) => {
                DetenerCaptura();
                this.DialogResult = DialogResult.Cancel;
            };

            this.Controls.Add(pbHuella);
            this.Controls.Add(lblInstrucciones);
            this.Controls.Add(btnCancelar);
            this.FormClosing += (s, e) => DetenerCaptura();
        }

        private void IniciarSecuenciaEnrolamiento()
        {
            if (_reader == null) return;

            // inicializar colección de FMDs para enrolamiento
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
                // Añadir la muestra actual a la colección
                _enrollmentFmds.Add(resultConversion.Data);

                this.Invoke(new Action(() =>
                {
                    lblInstrucciones.Text = $"Muestra registrada ({_enrollmentFmds.Count} de 4). Vuelva a apoyar.";
                }));

                // Cuando haya suficientes muestras, crear la plantilla final usando la API estática
                if (_enrollmentFmds.Count >= 4)
                {
                    DataResult<Fmd> createResult = Enrollment.CreateEnrollmentFmd(Constants.Formats.Fmd.DP_VERIFICATION, _enrollmentFmds);

                    if (createResult.ResultCode == Constants.ResultCode.DP_SUCCESS)
                    {
                        DetenerCaptura();

                        string templateBase64 = Fmd.SerializeXml(createResult.Data);

                        HuellaCapturada = new Huella
                        {
                            EmpleadoId = _empleadoId,
                            TemplateBiometrico = templateBase64
                        };

                        this.Invoke(new Action(() =>
                        {
                            MessageBox.Show("¡Huella enrolada exitosamente!", "Éxito", MessageBoxButtons.OK, MessageBoxIcon.Information);
                            this.DialogResult = DialogResult.OK;
                            this.Close();
                        }));
                    }
                    else
                    {
                        // Opcional: manejar fallo en creación de plantilla (se puede limpiar la lista y pedir reintento)
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
            // Copia rápida de array de bytes monocromáticos a la paleta en escala de grises
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

    }
}
