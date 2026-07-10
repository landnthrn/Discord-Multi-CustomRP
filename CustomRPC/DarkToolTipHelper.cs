using System.Drawing;
using System.Windows.Forms;

namespace CustomRPC
{
    static class DarkToolTipHelper
    {
        public static readonly Color Background = Color.FromArgb(45, 47, 53);
        public static readonly Color Foreground = Color.White;
        public static readonly Color Border = Color.FromArgb(80, 82, 88);
        const int MaxTooltipWidth = 420;

        public static void Configure(ToolTip toolTip)
        {
            toolTip.AutoPopDelay = int.MaxValue;
            toolTip.InitialDelay = 400;
            toolTip.ReshowDelay = 100;
            toolTip.ShowAlways = true;
            toolTip.OwnerDraw = true;
            toolTip.ToolTipIcon = ToolTipIcon.None;
            toolTip.ToolTipTitle = string.Empty;
            toolTip.Draw -= ToolTip_Draw;
            toolTip.Draw += ToolTip_Draw;
            toolTip.Popup -= ToolTip_Popup;
            toolTip.Popup += ToolTip_Popup;
        }

        static void ToolTip_Popup(object sender, PopupEventArgs e)
        {
            if (!(sender is ToolTip tip) || e.AssociatedControl == null)
                return;

            string text = tip.GetToolTip(e.AssociatedControl);
            if (string.IsNullOrEmpty(text))
                return;

            Size textSize = TextRenderer.MeasureText(
                text,
                SystemFonts.StatusFont,
                new Size(MaxTooltipWidth, int.MaxValue),
                TextFormatFlags.WordBreak);

            e.ToolTipSize = new Size(textSize.Width + 16, textSize.Height + 12);
        }

        static void ToolTip_Draw(object sender, DrawToolTipEventArgs e)
        {
            using (var background = new SolidBrush(Background))
            using (var border = new Pen(Border))
            {
                e.Graphics.FillRectangle(background, e.Bounds);
                e.Graphics.DrawRectangle(border, 0, 0, e.Bounds.Width - 1, e.Bounds.Height - 1);
            }

            Rectangle textRect = Rectangle.Inflate(e.Bounds, -8, -6);
            TextRenderer.DrawText(
                e.Graphics,
                e.ToolTipText,
                SystemFonts.StatusFont,
                textRect,
                Foreground,
                TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.WordBreak);
        }
    }
}
