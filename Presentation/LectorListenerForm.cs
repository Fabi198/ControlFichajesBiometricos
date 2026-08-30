using System;
using System.Windows.Forms;

namespace DevsFingerPrint.Presentation
{
    public class LectorListenerForm : Form
    {
        public LectorListenerForm()
        {
            // Forzamos visibilidad total para pruebas de hardware
            this.ShowInTaskbar = true;
            this.WindowState = FormWindowState.Normal;
            this.Visible = true;
            this.Text = "DevsFingerPrint - Panel de Prueba Lector";
            this.Width = 400;
            this.Height = 200;

            System.Diagnostics.Debug.WriteLine("[LOG LectorForm] Instancia creada y VISIBLE para pruebas.");
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
    }
}