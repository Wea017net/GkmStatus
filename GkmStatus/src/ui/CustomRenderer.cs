using System;
using System.Collections.Generic;
using System.Runtime.Versioning;
using System.Text;

namespace GkmStatus.src.ui
{
    [SupportedOSPlatform("windows")]
    public class MyRenderer(bool isBright, CustomColorTable colorTable) : ToolStripProfessionalRenderer(colorTable)
    {
        private readonly bool _isBright = isBright;
        private readonly CustomColorTable _colorTable = colorTable;

        protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
        {
            using var b = new SolidBrush(e.ToolStrip.BackColor);
            e.Graphics.FillRectangle(b, 0, 0, e.ToolStrip.Width, e.ToolStrip.Height);
        }

        protected override void OnRenderImageMargin(ToolStripRenderEventArgs e)
        {
            using var b = new SolidBrush(e.ToolStrip.BackColor);
            e.Graphics.FillRectangle(b, e.AffectedBounds);
        }

        protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
        {
            if (e.Image != null)
            {
                e.Graphics.DrawImage(e.Image, e.ImageRectangle);
            }
        }

        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            Rectangle r = new(Point.Empty, e.Item.Size);
            r.Width -= 1;
            r.Height -= 1;

            if (e.Item.Pressed)
            {
                using var b = new SolidBrush(_colorTable.HoverBgColor);
                e.Graphics.FillRectangle(b, r);

                Color borderColor = _isBright ? Color.Gray : Color.White;
                using var p = new Pen(borderColor, 1);
                e.Graphics.DrawRectangle(p, r);
            }
            else if (e.Item.Selected)
            {
                using var b = new SolidBrush(_colorTable.HoverBgColor);
                e.Graphics.FillRectangle(b, r);
            }
        }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            e.TextColor = _isBright ? Color.Black : Color.White;
            base.OnRenderItemText(e);
        }

        protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
        {
            Color line = _isBright ? Color.FromArgb(180, 180, 180) : Color.FromArgb(80, 80, 80);
            using var p = new Pen(line);
            int y = e.Item.Height / 2;
            e.Graphics.DrawLine(p, 30, y, e.Item.Width - 5, y);
        }

        protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
        {
            e.ArrowColor = _isBright ? Color.Black : Color.White;
            base.OnRenderArrow(e);
        }
    }

    [SupportedOSPlatform("windows")]
    public class CustomColorTable(bool isBright) : ProfessionalColorTable
    {
        public Color HoverBgColor { get; } = isBright ? Color.FromArgb(180, 200, 200, 200) : Color.FromArgb(60, 255, 255, 255);
        public Color CustomBorderColor { get; } = isBright ? Color.Gray : Color.White;

        public override Color MenuItemSelected => HoverBgColor;
        public override Color MenuItemSelectedGradientBegin => HoverBgColor;
        public override Color MenuItemSelectedGradientEnd => HoverBgColor;
        public override Color MenuItemPressedGradientBegin => HoverBgColor;
        public override Color MenuItemPressedGradientEnd => HoverBgColor;
        public override Color MenuItemPressedGradientMiddle => HoverBgColor;
        public override Color MenuItemBorder => CustomBorderColor;
        public override Color MenuBorder => CustomBorderColor;
    }
}
