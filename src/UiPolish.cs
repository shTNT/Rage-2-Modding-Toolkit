// UiPolish.cs - utilidades visuales: TopMost helpers + AccentBar con brillo animado.
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Rage2Toolkit
{
    public static class UiPolish
    {
        // (helpers TopMost eliminados tras revert - causaban bleeding visual)
    }

    public class AccentBar : Panel
    {
        Color _accent = Color.FromArgb(220, 80, 220);

        public override Color BackColor
        {
            get { return base.BackColor; }
            set { base.BackColor = value; _accent = value; Invalidate(); }
        }

        public AccentBar()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            int w = Width, h = Height;
            if (w <= 0 || h <= 0) return;

            // Color del fondo del padre (para fundir los extremos con el header).
            Color edge = Color.FromArgb(15, 15, 20);
            try { if (this.Parent != null && this.Parent.BackColor != Color.Empty) edge = this.Parent.BackColor; } catch { }

            // Gradiente horizontal: edge -> accent -> edge
            using (var lg = new LinearGradientBrush(new Rectangle(0, 0, w, h), edge, edge, LinearGradientMode.Horizontal))
            {
                var blend = new ColorBlend(3);
                blend.Colors = new[] { edge, _accent, edge };
                blend.Positions = new[] { 0f, 0.5f, 1f };
                lg.InterpolationColors = blend;
                g.FillRectangle(lg, 0, 0, w, h);
            }
        }
    }
}