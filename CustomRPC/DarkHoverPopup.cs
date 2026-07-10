using System;
using System.Drawing;
using System.Windows.Forms;

namespace CustomRPC
{
    /// <summary>
    /// Borderless dark popup for structured hover text (stays open while hovered).
    /// </summary>
    sealed class DarkHoverPopup : Form
    {
        const int MaxContentWidth = 380;

        readonly Padding defaultContentPadding = new Padding(10, 8, 10, 8);

        readonly Panel contentPanel;
        readonly Timer hideTimer;
        readonly Font baseFont;
        TooltipLine[] lines = Array.Empty<TooltipLine>();
        Padding contentPadding;
        Rectangle? clampBounds;

        public DarkHoverPopup()
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            TopMost = true;
            BackColor = DarkToolTipHelper.Background;
            AutoSize = false;
            contentPadding = defaultContentPadding;

            baseFont = SystemFonts.StatusFont;

            contentPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = DarkToolTipHelper.Background,
            };
            contentPanel.Paint += ContentPanel_Paint;
            Controls.Add(contentPanel);

            hideTimer = new Timer { Interval = 120 };
            hideTimer.Tick += (_, __) =>
            {
                hideTimer.Stop();
                Hide();
            };

            MouseEnter += (_, __) => hideTimer.Stop();
            MouseLeave += (_, __) => ScheduleHide();
            contentPanel.MouseEnter += (_, __) => hideTimer.Stop();
            contentPanel.MouseLeave += (_, __) => ScheduleHide();
        }

        protected override bool ShowWithoutActivation => true;

        protected override CreateParams CreateParams
        {
            get
            {
                const int WS_EX_NOACTIVATE = 0x08000000;
                const int WS_EX_TOOLWINDOW = 0x00000080;
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW;
                return cp;
            }
        }

        public void ShowLines(TooltipLine[] tooltipLines, Point screenLocation)
        {
            ShowLines(tooltipLines, screenLocation, null, null);
        }

        public void ShowLines(
            TooltipLine[] tooltipLines,
            Point screenLocation,
            Rectangle? clampToScreenBounds,
            Padding? padding)
        {
            hideTimer.Stop();
            lines = tooltipLines ?? Array.Empty<TooltipLine>();
            clampBounds = clampToScreenBounds;
            contentPadding = padding ?? defaultContentPadding;
            PositionAndShow(screenLocation);
        }

        public void ScheduleHide()
        {
            hideTimer.Stop();
            hideTimer.Start();
        }

        public void CancelHide() => hideTimer.Stop();

        public void HideNow()
        {
            hideTimer.Stop();
            Hide();
        }

        void ContentPanel_Paint(object sender, PaintEventArgs e) =>
            TooltipPainter.Paint(e.Graphics, lines, baseFont, contentPadding, MaxContentWidth);

        void PositionAndShow(Point screenLocation)
        {
            Size contentSize;
            using (var bitmap = new Bitmap(1, 1))
            using (var g = Graphics.FromImage(bitmap))
                contentSize = TooltipPainter.Layout(g, lines, baseFont, contentPadding, MaxContentWidth);

            ClientSize = contentSize;
            contentPanel.Invalidate();

            Rectangle area = Screen.FromPoint(screenLocation).WorkingArea;
            if (clampBounds.HasValue)
                area = Rectangle.Intersect(area, clampBounds.Value);

            int x = screenLocation.X + 12;
            int y = screenLocation.Y + 16;
            if (x + Width > area.Right)
                x = Math.Max(area.Left, area.Right - Width);
            if (x < area.Left)
                x = area.Left;
            if (y + Height > area.Bottom)
                y = Math.Max(area.Top, screenLocation.Y - Height - 8);
            if (y < area.Top)
                y = area.Top;

            Location = new Point(x, y);
            if (!Visible)
                Show();
            else
                BringToFront();
        }
    }
}
