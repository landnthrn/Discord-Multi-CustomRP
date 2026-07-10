using System.Drawing;
using System.Windows.Forms;

namespace CustomRPC
{
    /// <summary>
    /// Color scheme based on current mode.
    /// </summary>
    public static class CurrentColors
    {
        public static Color BgInactive => Color.FromArgb(47, 49, 54);
        public static Color BgHover => Color.FromArgb(52, 55, 60);
        public static Color BgActive => Color.FromArgb(57, 60, 67);
        public static Color CheckBg => Color.FromArgb(61, 73, 162);
        public static Color CheckHover => Color.FromArgb(72, 86, 193);
        public static Color TextInactive => Color.FromArgb(142, 146, 151);
        public static Color BgColor { get; private set; }
        public static Color BgButton { get; private set; }
        public static Color BgButtonMouseOver { get; private set; }
        public static Color BgButtonMouseDown { get; private set; }
        public static Color BgTextFields { get; private set; }
        public static Color BgTextFieldsSuccess { get; private set; }
        public static Color BgTextFieldsError { get; private set; }
        public static Color BgTextFieldsDuplicateId { get; private set; }
        public static Color TextColor { get; private set; }
        public static Color BgListSelected => Color.FromArgb(42, 44, 48);
        public static Color BgListSelectedInactive => BgListSelected;

        static CurrentColors()
        {
            Update();
        }

        /// <summary>
        /// Updates colors according to currently set dark mode setting.
        /// </summary>
        public static void Update()
        {
            if (Properties.Settings.Default.darkMode)
            {
                BgColor = Color.FromArgb(55, 57, 63);
                BgButton = Color.FromArgb(24, 25, 28);
                BgButtonMouseOver = Color.FromArgb(72, 86, 193);
                BgButtonMouseDown = Color.FromArgb(61, 73, 162);
                BgTextFields = Color.FromArgb(65, 68, 75);
                BgTextFieldsSuccess = Color.FromArgb(50, 150, 50);
                BgTextFieldsError = Color.FromArgb(150, 50, 50);
                BgTextFieldsDuplicateId = Color.FromArgb(130, 45, 45);
                TextColor = Color.White;
            }
            else
            {
                BgColor = Color.FromArgb(255, 255, 255);
                BgButton = SystemColors.Control;
                BgButtonMouseOver = SystemColors.ControlLight;
                BgButtonMouseDown = SystemColors.ControlDark;
                BgTextFields = Color.FromArgb(235, 237, 239);
                BgTextFieldsSuccess = Color.FromArgb(192, 255, 192);
                BgTextFieldsError = Color.FromArgb(255, 192, 192);
                BgTextFieldsDuplicateId = Color.FromArgb(255, 180, 180);
                TextColor = Color.FromName("ControlText");
            }
        }
    }

    internal class DarkModeColorTable : ProfessionalColorTable
    {
        public override Color MenuItemSelected => CurrentColors.BgHover;
        public override Color MenuItemPressedGradientBegin => CurrentColors.BgActive;
        public override Color MenuItemPressedGradientEnd => MenuItemPressedGradientBegin;
        public override Color MenuItemPressedGradientMiddle => MenuItemPressedGradientBegin;
        public override Color MenuItemSelectedGradientBegin => CurrentColors.BgHover;
        public override Color MenuItemSelectedGradientEnd => MenuItemSelectedGradientBegin;
        public override Color ImageMarginGradientBegin => CurrentColors.BgInactive;
        public override Color ImageMarginGradientMiddle => ImageMarginGradientBegin;
        public override Color ImageMarginGradientEnd => ImageMarginGradientBegin;
        public override Color CheckSelectedBackground => CurrentColors.CheckBg;
        public override Color CheckBackground => CurrentColors.CheckHover;
        public override Color MenuItemBorder => CurrentColors.BgActive;
    }

    internal class DarkModeRenderer : ToolStripProfessionalRenderer
    {
        public DarkModeRenderer() : base(new DarkModeColorTable()) { }

        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
        {
            if (e.ToolStrip is StatusStrip)
                return;

            base.OnRenderToolStripBorder(e);

            e.Graphics.DrawRectangle(new Pen(CurrentColors.BgActive, 2), e.AffectedBounds);
            e.Graphics.FillRectangle(new SolidBrush(CurrentColors.BgInactive), e.ConnectedArea);
        }
        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            e.TextColor = e.Item.ForeColor == Control.DefaultForeColor ? CurrentColors.TextColor : e.Item.ForeColor;

            base.OnRenderItemText(e);
        }

        protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
        {
            e.ToolStrip.BackColor = CurrentColors.BgInactive;

            base.OnRenderToolStripBackground(e);
        }

        protected override void OnRenderItemImage(ToolStripItemImageRenderEventArgs e)
        {
            base.OnRenderItemImage(e);

            if (e.Item.Tag == null)
                return;

            if (e.Item.Image.Tag == null || (string)e.Item.Image.Tag == "light")
            {
                e.Item.Image = Properties.Resources.globe_white;
                e.Item.Image.Tag = "dark";
            }
        }

        protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
        {
            e.ArrowColor = CurrentColors.TextColor;

            base.OnRenderArrow(e);
        }
    }

    internal static class ControlThemeHelper
    {
        public static void EnableListViewDoubleBuffer(ListView listView)
        {
            if (listView == null)
                return;

            typeof(Control).InvokeMember(
                "DoubleBuffered",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.SetProperty,
                null,
                listView,
                new object[] { true });
        }
    }

    internal class LightModeRenderer : ToolStripProfessionalRenderer
    {
        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
        {
            if (e.ToolStrip is StatusStrip)
                return;

            base.OnRenderToolStripBorder(e);
        }

        protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
        {
            e.ToolStrip.BackColor = Color.FromArgb(242, 243, 245);

            base.OnRenderToolStripBackground(e);
        }

        protected override void OnRenderItemImage(ToolStripItemImageRenderEventArgs e)
        {
            base.OnRenderItemImage(e);

            if (e.Item.Tag == null)
                return;

            if (e.Item.Image.Tag == null || (string)e.Item.Image.Tag == "dark")
            {
                e.Item.Image = Properties.Resources.globe;
                e.Item.Image.Tag = "light";
            }
        }
    }
}
