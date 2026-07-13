using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace CustomRPC
{
    /// <summary>
    /// MessageBox wrapper that suppresses the Windows beep and lays out choice buttons
    /// as Cancel (left) → No → Yes (right). OK-only dialogs use the system MessageBox.
    /// </summary>
    static class QuietMessageBox
    {
        const uint SPI_GETBEEP = 0x0001;
        const uint SPI_SETBEEP = 0x0002;

        [DllImport("user32.dll", SetLastError = true)]
        static extern bool SystemParametersInfo(uint uiAction, uint uiParam, IntPtr pvParam, uint fWinIni);

        [DllImport("user32.dll", SetLastError = true)]
        static extern bool SystemParametersInfo(uint uiAction, uint uiParam, ref int pvParam, uint fWinIni);

        public static DialogResult Show(string text) =>
            Show(null, text, string.Empty, MessageBoxButtons.OK, MessageBoxIcon.None, MessageBoxDefaultButton.Button1);

        public static DialogResult Show(string text, string caption) =>
            Show(null, text, caption, MessageBoxButtons.OK, MessageBoxIcon.None, MessageBoxDefaultButton.Button1);

        public static DialogResult Show(string text, string caption, MessageBoxButtons buttons) =>
            Show(null, text, caption, buttons, MessageBoxIcon.None, MessageBoxDefaultButton.Button1);

        public static DialogResult Show(string text, string caption, MessageBoxButtons buttons, MessageBoxIcon icon) =>
            Show(null, text, caption, buttons, icon, MessageBoxDefaultButton.Button1);

        public static DialogResult Show(
            string text,
            string caption,
            MessageBoxButtons buttons,
            MessageBoxIcon icon,
            MessageBoxDefaultButton defaultButton) =>
            Show(null, text, caption, buttons, icon, defaultButton);

        public static DialogResult Show(IWin32Window owner, string text) =>
            Show(owner, text, string.Empty, MessageBoxButtons.OK, MessageBoxIcon.None, MessageBoxDefaultButton.Button1);

        public static DialogResult Show(IWin32Window owner, string text, string caption) =>
            Show(owner, text, caption, MessageBoxButtons.OK, MessageBoxIcon.None, MessageBoxDefaultButton.Button1);

        public static DialogResult Show(IWin32Window owner, string text, string caption, MessageBoxButtons buttons) =>
            Show(owner, text, caption, buttons, MessageBoxIcon.None, MessageBoxDefaultButton.Button1);

        public static DialogResult Show(
            IWin32Window owner,
            string text,
            string caption,
            MessageBoxButtons buttons,
            MessageBoxIcon icon) =>
            Show(owner, text, caption, buttons, icon, MessageBoxDefaultButton.Button1);

        public static DialogResult Show(
            IWin32Window owner,
            string text,
            string caption,
            MessageBoxButtons buttons,
            MessageBoxIcon icon,
            MessageBoxDefaultButton defaultButton)
        {
            return WithSilentBeep(() =>
            {
                if (buttons == MessageBoxButtons.OK)
                    return MessageBox.Show(owner, text, caption, buttons, icon, defaultButton);

                using (var dialog = new ChoiceDialog(text, caption, buttons, icon, defaultButton))
                {
                    return owner != null ? dialog.ShowDialog(owner) : dialog.ShowDialog();
                }
            });
        }

        static DialogResult WithSilentBeep(Func<DialogResult> show)
        {
            int beepEnabled = 1;
            SystemParametersInfo(SPI_GETBEEP, 0, ref beepEnabled, 0);
            SystemParametersInfo(SPI_SETBEEP, 0, IntPtr.Zero, 0);
            try
            {
                return show();
            }
            finally
            {
                if (beepEnabled != 0)
                    SystemParametersInfo(SPI_SETBEEP, 1, IntPtr.Zero, 0);
            }
        }

        /// <summary>
        /// Choice dialog with button order Cancel → No/Retry → Yes/OK (left to right).
        /// </summary>
        sealed class ChoiceDialog : Form
        {
            const int ButtonWidth = 88;
            const int ButtonHeight = 26;
            const int ButtonGap = 8;
            const int EdgePadding = 14;

            public ChoiceDialog(
                string text,
                string caption,
                MessageBoxButtons buttons,
                MessageBoxIcon icon,
                MessageBoxDefaultButton defaultButton)
            {
                Text = string.IsNullOrEmpty(caption) ? Application.ProductName : caption;
                FormBorderStyle = FormBorderStyle.FixedDialog;
                MaximizeBox = false;
                MinimizeBox = false;
                ShowInTaskbar = false;
                StartPosition = FormStartPosition.CenterScreen;
                AutoScaleMode = AutoScaleMode.Font;
                KeyPreview = true;
                BackColor = CurrentColors.BgColor;
                ForeColor = CurrentColors.TextColor;

                Shown += (_, __) =>
                {
                    if (Owner != null)
                        CenterToParent();
                };

                var messageLabel = new Label
                {
                    AutoSize = true,
                    MaximumSize = new Size(420, 0),
                    Text = text ?? "",
                    Location = new Point(EdgePadding + (icon != MessageBoxIcon.None ? 48 : 0), EdgePadding),
                    ForeColor = CurrentColors.TextColor,
                };
                Controls.Add(messageLabel);

                if (icon != MessageBoxIcon.None)
                {
                    var iconBox = new PictureBox
                    {
                        Size = new Size(32, 32),
                        Location = new Point(EdgePadding, EdgePadding),
                        SizeMode = PictureBoxSizeMode.CenterImage,
                        Image = GetIconBitmap(icon),
                    };
                    Controls.Add(iconBox);
                }

                var buttonSpecs = BuildButtons(buttons);
                var buttonControls = new Button[buttonSpecs.Length];
                Button accept = null;
                Button cancel = null;

                for (int i = 0; i < buttonSpecs.Length; i++)
                {
                    var spec = buttonSpecs[i];
                    var button = new Button
                    {
                        Text = spec.Text,
                        DialogResult = spec.Result,
                        Size = new Size(ButtonWidth, ButtonHeight),
                        FlatStyle = Properties.Settings.Default.darkMode ? FlatStyle.Flat : FlatStyle.Standard,
                        UseVisualStyleBackColor = !Properties.Settings.Default.darkMode,
                        BackColor = Properties.Settings.Default.darkMode ? CurrentColors.BgButton : SystemColors.Control,
                        ForeColor = Properties.Settings.Default.darkMode ? CurrentColors.TextColor : SystemColors.ControlText,
                    };
                    if (Properties.Settings.Default.darkMode)
                    {
                        button.FlatAppearance.BorderSize = 0;
                        button.FlatAppearance.MouseOverBackColor = CurrentColors.BgButtonMouseOver;
                        button.FlatAppearance.MouseDownBackColor = CurrentColors.BgButtonMouseDown;
                    }

                    buttonControls[i] = button;
                    Controls.Add(button);

                    if (spec.IsAcceptCandidate)
                        accept = button;
                    if (spec.Result == DialogResult.Cancel)
                        cancel = button;
                }

                ApplyDefaultButton(buttonControls, buttonSpecs, defaultButton, buttons, ref accept);

                int contentRight = Math.Max(
                    messageLabel.Right,
                    icon != MessageBoxIcon.None ? EdgePadding + 32 : 0);
                int buttonsWidth = buttonControls.Length * ButtonWidth +
                    Math.Max(0, buttonControls.Length - 1) * ButtonGap;
                int clientWidth = Math.Max(contentRight + EdgePadding, buttonsWidth + EdgePadding * 2);
                int messageBottom = Math.Max(messageLabel.Bottom, icon != MessageBoxIcon.None ? EdgePadding + 32 : 0);
                int buttonTop = messageBottom + 18;
                int clientHeight = buttonTop + ButtonHeight + EdgePadding;

                ClientSize = new Size(clientWidth, clientHeight);

                int x = clientWidth - EdgePadding - buttonsWidth;
                for (int i = 0; i < buttonControls.Length; i++)
                {
                    buttonControls[i].Location = new Point(x, buttonTop);
                    x += ButtonWidth + ButtonGap;
                }

                if (accept != null)
                    AcceptButton = accept;
                if (cancel != null)
                    CancelButton = cancel;

                KeyDown += (_, e) =>
                {
                    if (e.KeyCode == Keys.Escape && cancel != null)
                    {
                        DialogResult = DialogResult.Cancel;
                        Close();
                    }
                };
            }

            static void ApplyDefaultButton(
                Button[] buttons,
                ButtonSpec[] specs,
                MessageBoxDefaultButton defaultButton,
                MessageBoxButtons boxButtons,
                ref Button accept)
            {
                // Map MessageBoxDefaultButton to DialogResult semantics (not visual index).
                DialogResult prefer;
                switch (boxButtons)
                {
                    case MessageBoxButtons.YesNo:
                        prefer = defaultButton == MessageBoxDefaultButton.Button2
                            ? DialogResult.No
                            : DialogResult.Yes;
                        break;
                    case MessageBoxButtons.YesNoCancel:
                        if (defaultButton == MessageBoxDefaultButton.Button2)
                            prefer = DialogResult.No;
                        else if (defaultButton == MessageBoxDefaultButton.Button3)
                            prefer = DialogResult.Cancel;
                        else
                            prefer = DialogResult.Yes;
                        break;
                    case MessageBoxButtons.OKCancel:
                        prefer = defaultButton == MessageBoxDefaultButton.Button2
                            ? DialogResult.Cancel
                            : DialogResult.OK;
                        break;
                    case MessageBoxButtons.RetryCancel:
                        prefer = defaultButton == MessageBoxDefaultButton.Button2
                            ? DialogResult.Cancel
                            : DialogResult.Retry;
                        break;
                    default:
                        prefer = DialogResult.None;
                        break;
                }

                for (int i = 0; i < specs.Length; i++)
                {
                    if (specs[i].Result == prefer)
                    {
                        accept = buttons[i];
                        return;
                    }
                }
            }

            static ButtonSpec[] BuildButtons(MessageBoxButtons buttons)
            {
                // Visual order: Cancel (far left) → No/Retry → Yes/OK (far right).
                switch (buttons)
                {
                    case MessageBoxButtons.YesNo:
                        return new[]
                        {
                            new ButtonSpec("&No", DialogResult.No, false),
                            new ButtonSpec("&Yes", DialogResult.Yes, true),
                        };
                    case MessageBoxButtons.YesNoCancel:
                        return new[]
                        {
                            new ButtonSpec("&Cancel", DialogResult.Cancel, false),
                            new ButtonSpec("&No", DialogResult.No, false),
                            new ButtonSpec("&Yes", DialogResult.Yes, true),
                        };
                    case MessageBoxButtons.OKCancel:
                        return new[]
                        {
                            new ButtonSpec("&Cancel", DialogResult.Cancel, false),
                            new ButtonSpec("&OK", DialogResult.OK, true),
                        };
                    case MessageBoxButtons.RetryCancel:
                        return new[]
                        {
                            new ButtonSpec("&Cancel", DialogResult.Cancel, false),
                            new ButtonSpec("&Retry", DialogResult.Retry, true),
                        };
                    case MessageBoxButtons.AbortRetryIgnore:
                        return new[]
                        {
                            new ButtonSpec("&Abort", DialogResult.Abort, false),
                            new ButtonSpec("&Retry", DialogResult.Retry, true),
                            new ButtonSpec("&Ignore", DialogResult.Ignore, false),
                        };
                    default:
                        return new[]
                        {
                            new ButtonSpec("&OK", DialogResult.OK, true),
                        };
                }
            }

            static Image GetIconBitmap(MessageBoxIcon icon)
            {
                Icon sys;
                switch (icon)
                {
                    case MessageBoxIcon.Error:
                        sys = SystemIcons.Error;
                        break;
                    case MessageBoxIcon.Warning:
                        sys = SystemIcons.Warning;
                        break;
                    case MessageBoxIcon.Information:
                        sys = SystemIcons.Information;
                        break;
                    case MessageBoxIcon.Question:
                        sys = SystemIcons.Question;
                        break;
                    default:
                        return null;
                }

                return sys.ToBitmap();
            }

            struct ButtonSpec
            {
                public readonly string Text;
                public readonly DialogResult Result;
                public readonly bool IsAcceptCandidate;

                public ButtonSpec(string text, DialogResult result, bool isAcceptCandidate)
                {
                    Text = text;
                    Result = result;
                    IsAcceptCandidate = isAcceptCandidate;
                }
            }
        }
    }
}
