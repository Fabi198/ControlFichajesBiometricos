using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace DevsFingerPrint.Presentation
{
    public class LectorListenerForm : Form
    {
        private readonly int mensajeBarraTareas;

        private const int WM_DEVICECHANGE = 0x0219;
        private const int DBT_DEVICEARRIVAL = 0x8000;
        private const int DBT_DEVICEREMOVECOMPLETE = 0x8004;
        private const int DBT_DEVNODES_CHANGED = 0x0007;

        public event EventHandler BarraTareasCreada;
        public event EventHandler DispositivoCambiado;

        public LectorListenerForm()
        {
            mensajeBarraTareas = RegisterWindowMessage("TaskbarCreated");
            this.ShowInTaskbar = false;
            this.WindowState = FormWindowState.Normal;
            this.FormBorderStyle = FormBorderStyle.None;

            // Mantiene la ventana siempre en primer plano (reemplaza al Always on Top de PowerToys)
            this.TopMost = true;

            this.Opacity = 0;
            this.Visible = true;
            this.ShowIcon = false;
            this.StartPosition = FormStartPosition.Manual;
            this.Location = new System.Drawing.Point(0, 0); // O tu posición deseada

            // Tamaño mínimo absoluto (1x1 píxel sin bordes)
            this.ClientSize = new System.Drawing.Size(1, 1);

            System.Diagnostics.Debug.WriteLine("[LOG LectorForm] Ventana en primer plano y tamaño mínimo configurados.");
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            System.Diagnostics.Debug.WriteLine($"[LOG LectorForm SUCCESS] Handle de ventana generado correctamente. IntPtr: {this.Handle}");
        }

        protected override void OnHandleDestroyed(EventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("[LOG LectorForm] El Handle de la ventana ha sido destruido.");
            base.OnHandleDestroyed(e);
        }

        protected override void WndProc(ref Message m)
        {
            if (Infrastructure.Services.CicloVidaAgente.EsMensajeBarraTareas(m.Msg, mensajeBarraTareas))
            {
                BarraTareasCreada?.Invoke(this, EventArgs.Empty);
            }
            else if (m.Msg == WM_DEVICECHANGE &&
                (m.WParam.ToInt32() == DBT_DEVICEARRIVAL ||
                 m.WParam.ToInt32() == DBT_DEVICEREMOVECOMPLETE ||
                 m.WParam.ToInt32() == DBT_DEVNODES_CHANGED))
            {
                DispositivoCambiado?.Invoke(this, EventArgs.Empty);
            }

            base.WndProc(ref m);
        }

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int RegisterWindowMessage(string lpString);
    }
}