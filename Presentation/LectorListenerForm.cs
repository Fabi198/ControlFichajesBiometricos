using System;
using System.Windows.Forms;

namespace DevsFingerPrint.Presentation
{
    public class LectorListenerForm : Form
    {
        public LectorListenerForm()
        {
            
            // Configuramos el formulario para que sea totalmente invisible y no aparezca en la barra de tareas
            this.ShowInTaskbar = false;
            this.WindowState = FormWindowState.Normal;
            this.FormBorderStyle = FormBorderStyle.FixedToolWindow;
            this.Opacity = 0;
            this.Visible = true;
            this.ShowIcon = true;
            this.StartPosition = FormStartPosition.Manual;
            this.Location = new System.Drawing.Point(-2000, -2000); // Fuera de la pantalla por seguridad

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