using System;
using System.Drawing;
using System.Windows.Forms;

namespace CustomRPC
{
    public partial class About : Form
    {
        public About()
        {
            InitializeComponent();

            WinApi.UseImmersiveDarkMode(Handle);

            BackColor = CurrentColors.BgColor;
            ForeColor = CurrentColors.TextColor;

            buttonClose.FlatStyle = Properties.Settings.Default.darkMode ? FlatStyle.Flat : FlatStyle.Standard;

            labelVersion.Text = VersionHelper.GetVersionString(Application.ProductVersion);
#if DEBUG
            labelVersion.Text += " (DEV)";
#endif

            SetupAuthorLinks();
        }

        void SetupAuthorLinks()
        {
            // Replace the plain Made-by label with a clickable author link.
            labelMadeBy.Visible = false;

            var linkUpstream = CreateAuthorLinkLabel(
                "Made by: maximmax42",
                "maximmax42",
                "https://github.com/maximmax42");
            linkUpstream.Location = labelMadeBy.Location;
            Controls.Add(linkUpstream);

            // Multi-RP title + author link, right-aligned under the original Made-by credit.
            // UseMnemonic=false so '&' shows literally (not as an accelerator).
            var labelForkTitle = new Label
            {
                AutoSize = true,
                Text = "Multi-RP's & Cycler",
                Font = labelTitle.Font,
                ForeColor = CurrentColors.TextColor,
                BackColor = Color.Transparent,
                TextAlign = ContentAlignment.MiddleRight,
                UseMnemonic = false,
            };
            labelForkTitle.Location = new Point(
                RightAlignX(labelForkTitle, labelTitle.Right),
                linkUpstream.Bottom + 18);
            Controls.Add(labelForkTitle);

            var linkFork = CreateAuthorLinkLabel(
                "Made by: landnthrn",
                "landnthrn",
                "https://github.com/landnthrn");
            linkFork.Location = new Point(
                RightAlignX(linkFork, linkUpstream.Right),
                labelForkTitle.Bottom + 6);
            Controls.Add(linkFork);

            // Bottom row: website | Close | version
            const int bottomGap = 16;
            int bottomY = linkFork.Bottom + 20;

            buttonClose.Top = bottomY;
            buttonClose.Left = Math.Max(12, (ClientSize.Width - buttonClose.Width) / 2);

            linkLabelWebsite.Top = buttonClose.Top + Math.Max(0, (buttonClose.Height - linkLabelWebsite.Height) / 2);
            linkLabelWebsite.Left = 8;

            labelVersion.Top = buttonClose.Top;
            labelVersion.Height = buttonClose.Height;
            labelVersion.Left = ClientSize.Width - labelVersion.Width - 8;

            ClientSize = new Size(ClientSize.Width, buttonClose.Bottom + bottomGap);
        }

        int RightAlignX(Control control, int rightEdge)
        {
            int width = control.PreferredSize.Width > 0 ? control.PreferredSize.Width : control.Width;
            return Math.Max(12, rightEdge - width);
        }

        LinkLabel CreateAuthorLinkLabel(string fullText, string linkText, string url)
        {
            var link = new LinkLabel
            {
                AutoSize = true,
                Text = fullText,
                Font = labelMadeBy.Font,
                BackColor = Color.Transparent,
                ForeColor = CurrentColors.TextColor,
                LinkBehavior = LinkBehavior.NeverUnderline,
                LinkColor = Color.FromArgb(88, 101, 242),
                ActiveLinkColor = Color.FromArgb(237, 66, 69),
                VisitedLinkColor = Color.FromArgb(88, 101, 242),
                UseMnemonic = false,
            };

            int start = fullText.IndexOf(linkText, StringComparison.Ordinal);
            if (start >= 0)
                link.Links.Add(start, linkText.Length, url);

            link.LinkClicked += AuthorLink_LinkClicked;
            return link;
        }

        void AuthorLink_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            string url = e.Link?.LinkData as string;
            if (!string.IsNullOrWhiteSpace(url))
                Utils.OpenInBrowser(url);
        }

        private void OpenWebsite(object sender, LinkLabelLinkClickedEventArgs e)
        {
            Utils.OpenInBrowser("https://www.customrp.xyz");
        }
    }
}
