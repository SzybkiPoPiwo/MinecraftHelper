using Drawing = System.Drawing;
using Forms = System.Windows.Forms;

namespace MinecraftHelper
{
    internal sealed class DarkTrayMenuColorTable : Forms.ProfessionalColorTable
    {
        private static readonly Drawing.Color Surface = Drawing.Color.FromArgb(16, 28, 44);
        private static readonly Drawing.Color SurfaceHover = Drawing.Color.FromArgb(24, 58, 86);
        private static readonly Drawing.Color Accent = Drawing.Color.FromArgb(46, 168, 255);
        private static readonly Drawing.Color Border = Drawing.Color.FromArgb(61, 88, 119);

        public override Drawing.Color ToolStripDropDownBackground => Surface;
        public override Drawing.Color MenuBorder => Border;
        public override Drawing.Color MenuItemBorder => Accent;
        public override Drawing.Color MenuItemSelected => SurfaceHover;
        public override Drawing.Color MenuItemSelectedGradientBegin => SurfaceHover;
        public override Drawing.Color MenuItemSelectedGradientEnd => SurfaceHover;
        public override Drawing.Color MenuItemPressedGradientBegin => SurfaceHover;
        public override Drawing.Color MenuItemPressedGradientMiddle => SurfaceHover;
        public override Drawing.Color MenuItemPressedGradientEnd => SurfaceHover;
        public override Drawing.Color ImageMarginGradientBegin => Surface;
        public override Drawing.Color ImageMarginGradientMiddle => Surface;
        public override Drawing.Color ImageMarginGradientEnd => Surface;
        public override Drawing.Color SeparatorDark => Border;
        public override Drawing.Color SeparatorLight => Border;
    }

    internal sealed class DarkTrayMenuRenderer : Forms.ToolStripProfessionalRenderer
    {
        private static readonly Drawing.Color Surface = Drawing.Color.FromArgb(16, 28, 44);
        private static readonly Drawing.Color SurfaceHover = Drawing.Color.FromArgb(24, 58, 86);
        private static readonly Drawing.Color SurfaceDisabled = Drawing.Color.FromArgb(19, 31, 46);
        private static readonly Drawing.Color Text = Drawing.Color.FromArgb(233, 244, 255);
        private static readonly Drawing.Color TextDisabled = Drawing.Color.FromArgb(112, 132, 159);
        private static readonly Drawing.Color Accent = Drawing.Color.FromArgb(46, 168, 255);
        private static readonly Drawing.Color Border = Drawing.Color.FromArgb(61, 88, 119);

        public DarkTrayMenuRenderer()
            : base(new DarkTrayMenuColorTable())
        {
            RoundedEdges = false;
        }

        protected override void OnRenderToolStripBackground(Forms.ToolStripRenderEventArgs e)
        {
            using var brush = new Drawing.SolidBrush(Surface);
            e.Graphics.FillRectangle(brush, e.AffectedBounds);
        }

        protected override void OnRenderImageMargin(Forms.ToolStripRenderEventArgs e)
        {
            using var brush = new Drawing.SolidBrush(Surface);
            e.Graphics.FillRectangle(brush, e.AffectedBounds);
        }

        protected override void OnRenderMenuItemBackground(Forms.ToolStripItemRenderEventArgs e)
        {
            Drawing.Rectangle bounds = new Drawing.Rectangle(Drawing.Point.Empty, e.Item.Size);
            Drawing.Color background = e.Item.Enabled
                ? e.Item.Selected ? SurfaceHover : Surface
                : SurfaceDisabled;

            using (var brush = new Drawing.SolidBrush(background))
                e.Graphics.FillRectangle(brush, bounds);

            if (e.Item.Enabled && e.Item.Selected && bounds.Width > 1 && bounds.Height > 1)
            {
                using var pen = new Drawing.Pen(Accent);
                e.Graphics.DrawRectangle(pen, 0, 0, bounds.Width - 1, bounds.Height - 1);
            }
        }

        protected override void OnRenderItemText(Forms.ToolStripItemTextRenderEventArgs e)
        {
            e.TextColor = e.Item.Enabled ? Text : TextDisabled;
            base.OnRenderItemText(e);
        }

        protected override void OnRenderSeparator(Forms.ToolStripSeparatorRenderEventArgs e)
        {
            using (var background = new Drawing.SolidBrush(Surface))
                e.Graphics.FillRectangle(background, new Drawing.Rectangle(Drawing.Point.Empty, e.Item.Size));

            int y = e.Item.Height / 2;
            using var pen = new Drawing.Pen(Border);
            e.Graphics.DrawLine(pen, 6, y, Math.Max(6, e.Item.Width - 7), y);
        }

        protected override void OnRenderToolStripBorder(Forms.ToolStripRenderEventArgs e)
        {
            Drawing.Rectangle bounds = new Drawing.Rectangle(Drawing.Point.Empty, e.ToolStrip.Size);
            if (bounds.Width <= 1 || bounds.Height <= 1)
                return;

            using var pen = new Drawing.Pen(Border);
            e.Graphics.DrawRectangle(pen, 0, 0, bounds.Width - 1, bounds.Height - 1);
        }
    }
}
