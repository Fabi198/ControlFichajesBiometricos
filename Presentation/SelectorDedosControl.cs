using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace DevsFingerPrint.Presentation
{
    public enum ManoTipo
    {
        Izquierda,
        Derecha
    }

    public enum DedoTipo
    {
        Pulgar,
        Indice,
        Medio,
        Anular,
        Menique
    }

    public class DedoSeleccionadoEventArgs : EventArgs
    {
        public ManoTipo Mano { get; private set; }
        public DedoTipo Dedo { get; private set; }

        public DedoSeleccionadoEventArgs(
            ManoTipo mano,
            DedoTipo dedo)
        {
            Mano = mano;
            Dedo = dedo;
        }
    }

    public class SelectorDedosControl : UserControl
    {
        // ============================================================
        // DIMENSIONES VIRTUALES
        // ============================================================

        private const float AnchoVirtual = 720f;
        private const float AltoVirtual = 400f;

        // ============================================================
        // ESCALA DE LAS MANOS
        // ============================================================

        private const float EscalaManos = 0.82f;

        private const float CentroEscalaX = 142f;
        private const float CentroEscalaY = 200f;

        // ============================================================
        // GEOMETRIA
        // ============================================================

        private const float EjeEspejo = 284f;

        private const float DesplazamientoXDerecha = 395f;
        private const float DesplazamientoXIzquierda = 21f;
        private const float DesplazamientoY = 10f;

        // ============================================================
        // SELECTORES
        // ============================================================

        private const float AnchoSelector = 14f;
        private const float AltoSelector = 38f;

        // ============================================================
        // ESCALA DEL SELECTOR INTERIOR
        // ============================================================

        /*
         * Se conserva por si posteriormente queremos utilizar
         * selectores basados en la forma del dedo.
         */
        private const float EscalaSeleccion = 0.72f;

        // ============================================================
        // COLORES
        // ============================================================

        private static readonly Color ColorContorno =
            Color.FromArgb(
                220,
                140,
                152,
                180);

        private static readonly Color ColorHover =
            Color.FromArgb(
                120,
                72,
                95,
                150);

        private static readonly Color ColorSeleccionado =
            Color.FromArgb(
                215,
                61,
                114,
                230);

        private static readonly Color ColorBordeSeleccionado =
            Color.FromArgb(
                255,
                45,
                100,
                220);

        // ============================================================
        // CLASE INTERNA
        // ============================================================

        private class ZonaDedo
        {
            public ManoTipo Mano;
            public DedoTipo Dedo;

            /*
             * Zona pequeña que se utiliza para hover y click.
             */
            public GraphicsPath Ruta;
        }

        // ============================================================
        // CAMPOS
        // ============================================================

        private readonly GraphicsPath _contornoDerecha;
        private readonly GraphicsPath _contornoIzquierda;

        private readonly List<ZonaDedo> _zonas =
            new List<ZonaDedo>();

        private ZonaDedo _zonaHover;
        private ZonaDedo _zonaSeleccionada;

        // ============================================================
        // EVENTO
        // ============================================================

        public event EventHandler<DedoSeleccionadoEventArgs>
            DedoSeleccionado;

        // ============================================================
        // CONSTRUCTOR
        // ============================================================

        public SelectorDedosControl()
        {
            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.UserPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw,
                true);

            BackColor =
                Color.FromArgb(
                    10,
                    17,
                    32);

            MinimumSize =
                new Size(
                    300,
                    170);

            // ========================================================
            // CONTORNOS DE LAS MANOS
            // ========================================================

            var puntosContorno =
                ObtenerContornoBase();

            var puntosContornoEscalados =
                Escalar(
                    puntosContorno,
                    EscalaManos,
                    CentroEscalaX,
                    CentroEscalaY);

            // --------------------------------------------------------
            // MANO DERECHA
            // --------------------------------------------------------

            _contornoDerecha =
                new GraphicsPath();

            _contornoDerecha.AddClosedCurve(
                Trasladar(
                    puntosContornoEscalados,
                    DesplazamientoXDerecha,
                    DesplazamientoY));

            // --------------------------------------------------------
            // MANO IZQUIERDA
            // --------------------------------------------------------

            _contornoIzquierda =
                new GraphicsPath();

            _contornoIzquierda.AddClosedCurve(
                Trasladar(
                    Espejar(
                        puntosContornoEscalados),
                    DesplazamientoXIzquierda,
                    DesplazamientoY));

            // ========================================================
            // SELECTORES
            // ========================================================

            CrearZonasDedos();
        }

        // ============================================================
        // CONTORNO DE LA MANO
        // ============================================================

        private static PointF[] ObtenerContornoBase()
        {
            return new[]
            {
                // ----------------------------------------------------
                // PARTE INFERIOR
                // ----------------------------------------------------

                new PointF(70, 330),
                new PointF(48, 270),
                new PointF(45, 235),

                // ----------------------------------------------------
                // PULGAR
                // ----------------------------------------------------

                new PointF(30, 222),
                new PointF(18, 205),
                new PointF(12, 185),
                new PointF(16, 175),
                new PointF(32, 180),
                new PointF(55, 188),
                new PointF(95, 195),

                // ----------------------------------------------------
                // INDICE
                // ----------------------------------------------------

                new PointF(104, 85),
                new PointF(146, 85),
                new PointF(148, 195),

                // ----------------------------------------------------
                // MEDIO
                // ----------------------------------------------------

                new PointF(150, 45),
                new PointF(191, 45),
                new PointF(193, 190),

                // ----------------------------------------------------
                // ANULAR
                // ----------------------------------------------------

                new PointF(194, 68),
                new PointF(233, 68),
                new PointF(235, 195),

                // ----------------------------------------------------
                // MEÑIQUE
                // ----------------------------------------------------

                new PointF(236, 110),
                new PointF(269, 110),
                new PointF(272, 180),

                // ----------------------------------------------------
                // LATERAL
                // ----------------------------------------------------

                new PointF(268, 260),
                new PointF(222, 330)
            };
        }

        // ============================================================
        // CREAR ZONAS DE LOS DEDOS
        // ============================================================

        private void CrearZonasDedos()
        {
            CrearSelector(
                ManoTipo.Derecha,
                DedoTipo.Pulgar,
                false);

            CrearSelector(
                ManoTipo.Derecha,
                DedoTipo.Indice,
                false);

            CrearSelector(
                ManoTipo.Derecha,
                DedoTipo.Medio,
                false);

            CrearSelector(
                ManoTipo.Derecha,
                DedoTipo.Anular,
                false);

            CrearSelector(
                ManoTipo.Derecha,
                DedoTipo.Menique,
                false);

            // --------------------------------------------------------
            // MANO IZQUIERDA
            // --------------------------------------------------------

            CrearSelector(
                ManoTipo.Izquierda,
                DedoTipo.Pulgar,
                true);

            CrearSelector(
                ManoTipo.Izquierda,
                DedoTipo.Indice,
                true);

            CrearSelector(
                ManoTipo.Izquierda,
                DedoTipo.Medio,
                true);

            CrearSelector(
                ManoTipo.Izquierda,
                DedoTipo.Anular,
                true);

            CrearSelector(
                ManoTipo.Izquierda,
                DedoTipo.Menique,
                true);
        }

        // ============================================================
        // CREAR SELECTOR
        // ============================================================

        private void CrearSelector(
            ManoTipo mano,
            DedoTipo dedo,
            bool espejar)
        {
            float centroX = 0f;
            float centroY = 0f;

            // --------------------------------------------------------
            // POSICION DE LA HUELLA
            // --------------------------------------------------------

            switch (dedo)
            {
                case DedoTipo.Indice:

                    centroX = 125f;
                    centroY = 106f;

                    break;

                case DedoTipo.Medio:

                    centroX = 170f;
                    centroY = 70f;

                    break;

                case DedoTipo.Anular:

                    centroX = 214f;
                    centroY = 92f;

                    break;

                case DedoTipo.Menique:

                    centroX = 252f;
                    centroY = 130f;

                    break;

                case DedoTipo.Pulgar:

                    centroX = 48f;
                    centroY = 193f;

                    break;
            }

            // --------------------------------------------------------
            // ESCALA DE LA MANO
            // --------------------------------------------------------

            centroX =
                CentroEscalaX +
                (centroX - CentroEscalaX) *
                EscalaManos;

            centroY =
                CentroEscalaY +
                (centroY - CentroEscalaY) *
                EscalaManos;

            // --------------------------------------------------------
            // ESPEJO
            // --------------------------------------------------------

            if (espejar)
            {
                centroX =
                    EjeEspejo - centroX;
            }

            // --------------------------------------------------------
            // POSICION FINAL
            // --------------------------------------------------------

            float desplazamientoX =
                mano == ManoTipo.Derecha
                    ? DesplazamientoXDerecha
                    : DesplazamientoXIzquierda;

            centroX +=
                desplazamientoX;

            centroY +=
                DesplazamientoY;

            // --------------------------------------------------------
            // FORMA DEL SELECTOR
            // --------------------------------------------------------

            GraphicsPath ruta;

            if (dedo == DedoTipo.Pulgar)
            {
                ruta =
                    CrearSelectorPulgar(
                        centroX,
                        centroY);
            }
            else
            {
                ruta =
                    CrearSelectorVertical(
                        centroX,
                        centroY);
            }

            // --------------------------------------------------------
            // GUARDAR
            // --------------------------------------------------------

            _zonas.Add(
                new ZonaDedo
                {
                    Mano = mano,
                    Dedo = dedo,
                    Ruta = ruta
                });
        }

        // ============================================================
        // SELECTOR VERTICAL
        // ============================================================

        private static GraphicsPath CrearSelectorVertical(
            float centroX,
            float centroY)
        {
            float ancho =
                AnchoSelector;

            float alto =
                AltoSelector;

            float x =
                centroX -
                ancho / 2f;

            float y =
                centroY -
                alto / 2f;

            GraphicsPath ruta =
                new GraphicsPath();

            // --------------------------------------------------------
            // PARTE SUPERIOR
            // --------------------------------------------------------

            ruta.AddArc(
                x,
                y,
                ancho,
                ancho,
                180f,
                180f);

            // --------------------------------------------------------
            // LADO DERECHO
            // --------------------------------------------------------

            ruta.AddLine(
                x + ancho,
                y + ancho / 2f,
                x + ancho,
                y + alto - ancho / 2f);

            // --------------------------------------------------------
            // PARTE INFERIOR
            // --------------------------------------------------------

            ruta.AddArc(
                x,
                y + alto - ancho,
                ancho,
                ancho,
                0f,
                180f);

            // --------------------------------------------------------
            // LADO IZQUIERDO
            // --------------------------------------------------------

            ruta.AddLine(
                x,
                y + alto - ancho / 2f,
                x,
                y + ancho / 2f);

            ruta.CloseFigure();

            return ruta;
        }

        // ============================================================
        // SELECTOR DEL PULGAR
        // ============================================================

        private static GraphicsPath CrearSelectorPulgar(
            float centroX,
            float centroY)
        {
            float ancho = 32f;
            float alto = 14f;

            float x =
                centroX -
                ancho / 2f;

            float y =
                centroY -
                alto / 2f;

            GraphicsPath ruta =
                new GraphicsPath();

            // --------------------------------------------------------
            // EXTREMO IZQUIERDO
            // --------------------------------------------------------

            ruta.AddArc(
                x,
                y,
                alto,
                alto,
                90f,
                180f);

            // --------------------------------------------------------
            // PARTE SUPERIOR
            // --------------------------------------------------------

            ruta.AddLine(
                x + alto / 2f,
                y,
                x + ancho - alto / 2f,
                y);

            // --------------------------------------------------------
            // EXTREMO DERECHO
            // --------------------------------------------------------

            ruta.AddArc(
                x + ancho - alto,
                y,
                alto,
                alto,
                270f,
                180f);

            // --------------------------------------------------------
            // PARTE INFERIOR
            // --------------------------------------------------------

            ruta.AddLine(
                x + ancho - alto / 2f,
                y + alto,
                x + alto / 2f,
                y + alto);

            ruta.CloseFigure();

            // --------------------------------------------------------
            // INCLINACION DEL PULGAR
            // --------------------------------------------------------

            using (Matrix matriz =
                new Matrix())
            {
                matriz.RotateAt(
                    -35f,
                    new PointF(
                        centroX,
                        centroY));

                ruta.Transform(matriz);
            }

            return ruta;
        }

        // ============================================================
        // PAINT
        // ============================================================

        protected override void OnPaint(
            PaintEventArgs e)
        {
            base.OnPaint(e);

            if (ClientSize.Width <= 0 ||
                ClientSize.Height <= 0)
            {
                return;
            }

            Graphics g =
                e.Graphics;

            g.SmoothingMode =
                SmoothingMode.AntiAlias;

            // ========================================================
            // ESCALA UNIFORME
            // ========================================================

            float escalaX =
                ClientSize.Width /
                AnchoVirtual;

            float escalaY =
                ClientSize.Height /
                AltoVirtual;

            float escala =
                Math.Min(
                    escalaX,
                    escalaY);

            // ========================================================
            // CENTRAR
            // ========================================================

            float offsetX =
                (ClientSize.Width -
                 (AnchoVirtual * escala)) /
                2f;

            float offsetY =
                (ClientSize.Height -
                 (AltoVirtual * escala)) /
                2f;

            g.TranslateTransform(
                offsetX,
                offsetY);

            g.ScaleTransform(
                escala,
                escala);

            // ========================================================
            // FONDO
            // ========================================================

            using (SolidBrush fondo =
                new SolidBrush(
                    BackColor))
            {
                g.FillRectangle(
                    fondo,
                    0,
                    0,
                    AnchoVirtual,
                    AltoVirtual);
            }

            // ========================================================
            // HOVER / SELECCION
            // ========================================================

            foreach (var zona in _zonas)
            {
                // ----------------------------------------------------
                // SELECCIONADO
                // ----------------------------------------------------

                if (zona == _zonaSeleccionada)
                {
                    using (SolidBrush pincel =
                        new SolidBrush(
                            ColorSeleccionado))
                    {
                        g.FillPath(
                            pincel,
                            zona.Ruta);
                    }
                }

                // ----------------------------------------------------
                // HOVER
                // ----------------------------------------------------

                else if (zona == _zonaHover)
                {
                    using (SolidBrush pincel =
                        new SolidBrush(
                            ColorHover))
                    {
                        g.FillPath(
                            pincel,
                            zona.Ruta);
                    }
                }
            }

            // ========================================================
            // CONTORNOS DE LAS MANOS
            // ========================================================

            using (Pen lapiz =
                new Pen(
                    ColorContorno,
                    2f))
            {
                lapiz.DashStyle =
                    DashStyle.Dot;

                g.DrawPath(
                    lapiz,
                    _contornoDerecha);

                g.DrawPath(
                    lapiz,
                    _contornoIzquierda);
            }

            // ========================================================
            // BORDE DEL SELECCIONADO
            // ========================================================

            if (_zonaSeleccionada != null)
            {
                using (Pen borde =
                    new Pen(
                        ColorBordeSeleccionado,
                        2f))
                {
                    g.DrawPath(
                        borde,
                        _zonaSeleccionada.Ruta);
                }
            }

            g.ResetTransform();
        }

        // ============================================================
        // MOUSE MOVE
        // ============================================================

        protected override void OnMouseMove(
            MouseEventArgs e)
        {
            base.OnMouseMove(e);

            PointF punto =
                ConvertirAVirtual(
                    e.Location);

            ZonaDedo nueva =
                _zonas.Find(
                    delegate (ZonaDedo z)
                    {
                        return z.Ruta.IsVisible(
                            punto);
                    });

            if (nueva != _zonaHover)
            {
                _zonaHover =
                    nueva;

                Cursor =
                    nueva != null
                        ? Cursors.Hand
                        : Cursors.Default;

                Invalidate();
            }
        }

        // ============================================================
        // MOUSE LEAVE
        // ============================================================

        protected override void OnMouseLeave(
            EventArgs e)
        {
            base.OnMouseLeave(e);

            if (_zonaHover != null)
            {
                _zonaHover = null;

                Cursor =
                    Cursors.Default;

                Invalidate();
            }
        }

        // ============================================================
        // CLICK
        // ============================================================

        protected override void OnMouseClick(
            MouseEventArgs e)
        {
            base.OnMouseClick(e);

            if (e.Button != MouseButtons.Left)
            {
                return;
            }

            PointF punto =
                ConvertirAVirtual(
                    e.Location);

            ZonaDedo clic =
                _zonas.Find(
                    delegate (ZonaDedo z)
                    {
                        return z.Ruta.IsVisible(
                            punto);
                    });

            if (clic == null)
            {
                return;
            }

            _zonaSeleccionada =
                clic;

            Invalidate();

            if (DedoSeleccionado != null)
            {
                DedoSeleccionado(
                    this,
                    new DedoSeleccionadoEventArgs(
                        clic.Mano,
                        clic.Dedo));
            }
        }

        // ============================================================
        // SELECCIONAR PROGRAMATICAMENTE
        // ============================================================

        public void Seleccionar(
            ManoTipo mano,
            DedoTipo dedo)
        {
            _zonaSeleccionada =
                _zonas.Find(
                    delegate (ZonaDedo z)
                    {
                        return
                            z.Mano == mano &&
                            z.Dedo == dedo;
                    });

            Invalidate();
        }

        // ============================================================
        // LIMPIAR SELECCION
        // ============================================================

        public void LimpiarSeleccion()
        {
            _zonaSeleccionada = null;

            Invalidate();
        }

        // ============================================================
        // CONVERTIR COORDENADAS
        // ============================================================

        private PointF ConvertirAVirtual(
            Point puntoControl)
        {
            float escalaX =
                ClientSize.Width /
                AnchoVirtual;

            float escalaY =
                ClientSize.Height /
                AltoVirtual;

            float escala =
                Math.Min(
                    escalaX,
                    escalaY);

            float offsetX =
                (ClientSize.Width -
                 (AnchoVirtual * escala)) /
                2f;

            float offsetY =
                (ClientSize.Height -
                 (AltoVirtual * escala)) /
                2f;

            return new PointF(
                (puntoControl.X - offsetX) /
                escala,

                (puntoControl.Y - offsetY) /
                escala);
        }

        // ============================================================
        // ESCALAR
        // ============================================================

        private static PointF[] Escalar(
            PointF[] puntos,
            float escala,
            float centroX,
            float centroY)
        {
            PointF[] resultado =
                new PointF[puntos.Length];

            for (int i = 0;
                 i < puntos.Length;
                 i++)
            {
                resultado[i] =
                    new PointF(
                        centroX +
                        (puntos[i].X - centroX) *
                        escala,

                        centroY +
                        (puntos[i].Y - centroY) *
                        escala);
            }

            return resultado;
        }

        // ============================================================
        // ESPEJAR
        // ============================================================

        private static PointF[] Espejar(
            PointF[] puntos)
        {
            PointF[] resultado =
                new PointF[puntos.Length];

            for (int i = 0;
                 i < puntos.Length;
                 i++)
            {
                resultado[i] =
                    new PointF(
                        EjeEspejo -
                        puntos[i].X,
                        puntos[i].Y);
            }

            return resultado;
        }

        // ============================================================
        // TRASLADAR
        // ============================================================

        private static PointF[] Trasladar(
            PointF[] puntos,
            float dx,
            float dy)
        {
            PointF[] resultado =
                new PointF[puntos.Length];

            for (int i = 0;
                 i < puntos.Length;
                 i++)
            {
                resultado[i] =
                    new PointF(
                        puntos[i].X + dx,
                        puntos[i].Y + dy);
            }

            return resultado;
        }

        // ============================================================
        // DISPOSE
        // ============================================================

        protected override void Dispose(
            bool disposing)
        {
            if (disposing)
            {
                if (_contornoDerecha != null)
                {
                    _contornoDerecha.Dispose();
                }

                if (_contornoIzquierda != null)
                {
                    _contornoIzquierda.Dispose();
                }

                foreach (ZonaDedo zona in _zonas)
                {
                    if (zona.Ruta != null)
                    {
                        zona.Ruta.Dispose();
                    }
                }

                _zonas.Clear();
            }

            base.Dispose(disposing);
        }
    }
}