using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml.Serialization;
using DiscordRPC;
using WinButton = System.Windows.Forms.Button;
using WinTimer = System.Windows.Forms.Timer;

namespace CustomRPC
{
    public partial class MainForm
    {
        const int CyclePresetSwapStepDelayMs = 3000;
        const int CycleConnectWaitTimeoutMs = 15000;

        Panel panelSeparator5;
        Panel panelSeparator6;
        CheckBox checkBoxAlternatingEnabled;
        Label labelAlternatingPresets;
        WinButton buttonAlternatingBrowse;
        TextBox textBoxAlternatingFolder;
        ComboBox comboBoxAlternatingType;
        Label labelAlternatingSwitchInterval;
        NumericUpDown numericUpDownAlternatingMinutes;
        Label labelAlternatingMinutes;
        NumericUpDown numericUpDownAlternatingSeconds;
        Label labelAlternatingSeconds;
        Label labelCycleMode;
        ComboBox comboBoxCycleMode;
        Label labelCyclingActiveBanner;
        DarkHoverPopup alternatingPresetsHoverPopup;
        DarkHoverPopup cycleModeHoverPopup;
        DarkHoverPopup switchIntervalHoverPopup;
        DarkHoverPopup cyclingActiveBannerHoverPopup;
        WinTimer alternatingPresetsTimer;

        int alternatingPresetIndex;
        bool syncingAlternatingUi;
        bool alternatingApplyInProgress;
        bool cyclePresetSwapInProgress;
        bool cancelCycleAfterCurrentStep;
        List<CycleSwapStep> pendingCycleSteps;
        string pendingCycleTargetPath;
        /// <summary>Preset file currently shown / settled during cycling (for the menu status label).</summary>
        string activeCyclePresetPath;
        /// <summary>Source preset path while a cycle transition is in progress.</summary>
        string cycleTransitionFromPath;
        List<PresenceSlot> cycleBaselineSlots;
        string cycleBaselineSelectedId;
        /// <summary>True while Autoconnect is running after enabling/disabling Cycle RP's — keeps Connect All greyed out.</summary>
        bool cycleAutoconnectInProgress;
        /// <summary>
        /// Tray Disconnect cancelled Cycle RP's; tray Reconnect should turn it back on afterward.
        /// </summary>
        bool trayReconnectShouldRestoreCycle;
        string legacyTrayLastConnectedSlotId;

        sealed class CycleSwapStep
        {
            public PresenceSlot Incoming;
            public string OutgoingSlotId;
        }

        void SetupAlternatingPresetsSection()
        {
            panelSeparator5 = new Panel
            {
                Name = "panelSeparator5",
                Height = 1,
                BackColor = CurrentColors.TextColor,
            };

            panelSeparator6 = new Panel
            {
                Name = "panelSeparator6",
                Height = 1,
                BackColor = CurrentColors.TextColor,
            };

            checkBoxAlternatingEnabled = new CheckBox
            {
                AutoSize = true,
                Text = "",
                Margin = Padding.Empty,
            };
            checkBoxAlternatingEnabled.CheckedChanged += CheckBoxAlternatingEnabled_CheckedChanged;

            labelAlternatingPresets = new Label
            {
                AutoSize = true,
                Text = ActivitiesUiText.CyclePresetsTitle,
                Cursor = Cursors.Default,
                Margin = Padding.Empty,
            };
            labelAlternatingPresets.MouseEnter += AlternatingPresetsTitle_MouseEnter;
            labelAlternatingPresets.MouseLeave += AlternatingPresetsTitle_MouseLeave;
            labelAlternatingPresets.Click += AlternatingPresetsTitle_Click;
            labelAlternatingPresets.Cursor = Cursors.Default;

            alternatingPresetsHoverPopup = new DarkHoverPopup();
            alternatingPresetsHoverPopup.MouseEnter += (_, __) => alternatingPresetsHoverPopup.CancelHide();
            alternatingPresetsHoverPopup.MouseLeave += (_, __) => alternatingPresetsHoverPopup.ScheduleHide();

            cycleModeHoverPopup = new DarkHoverPopup();
            cycleModeHoverPopup.MouseEnter += (_, __) => cycleModeHoverPopup.CancelHide();
            cycleModeHoverPopup.MouseLeave += (_, __) => cycleModeHoverPopup.ScheduleHide();

            switchIntervalHoverPopup = new DarkHoverPopup();
            switchIntervalHoverPopup.MouseEnter += (_, __) => switchIntervalHoverPopup.CancelHide();
            switchIntervalHoverPopup.MouseLeave += (_, __) => switchIntervalHoverPopup.ScheduleHide();

            cyclingActiveBannerHoverPopup = new DarkHoverPopup();
            cyclingActiveBannerHoverPopup.MouseEnter += (_, __) => cyclingActiveBannerHoverPopup.CancelHide();
            cyclingActiveBannerHoverPopup.MouseLeave += (_, __) => cyclingActiveBannerHoverPopup.ScheduleHide();

            buttonAlternatingBrowse = CreateActionButton("Browse…", ButtonAlternatingBrowse_Click);
            buttonAlternatingBrowse.Margin = new Padding(0, 0, 6, 0);

            textBoxAlternatingFolder = new TextBox
            {
                ReadOnly = true,
                Width = 140,
                Margin = new Padding(0, 1, 6, 0),
            };

            comboBoxAlternatingType = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Width = 58,
                Margin = new Padding(0, 1, 6, 0),
            };
            comboBoxAlternatingType.Items.AddRange(new object[] { ".cmrp", ".crp" });
            comboBoxAlternatingType.SelectedIndexChanged += ComboBoxAlternatingType_SelectedIndexChanged;

            labelAlternatingSwitchInterval = new Label
            {
                AutoSize = true,
                Text = "Switch Interval",
                Cursor = Cursors.Default,
                Margin = Padding.Empty,
            };
            labelAlternatingSwitchInterval.MouseEnter += SwitchIntervalLabel_MouseEnter;
            labelAlternatingSwitchInterval.MouseLeave += SwitchIntervalLabel_MouseLeave;

            numericUpDownAlternatingMinutes = new NumericUpDown
            {
                Minimum = 0,
                Maximum = 1440,
                DecimalPlaces = 0,
                Width = 48,
                TextAlign = HorizontalAlignment.Left,
                Margin = new Padding(0, 1, 4, 0),
            };
            numericUpDownAlternatingMinutes.ValueChanged += NumericUpDownAlternatingInterval_ValueChanged;

            labelAlternatingMinutes = new Label
            {
                AutoSize = true,
                Text = "min",
                Margin = Padding.Empty,
            };

            numericUpDownAlternatingSeconds = new NumericUpDown
            {
                Minimum = 0,
                Maximum = 59,
                DecimalPlaces = 0,
                Width = 48,
                TextAlign = HorizontalAlignment.Left,
                Margin = new Padding(6, 1, 4, 0),
            };
            numericUpDownAlternatingSeconds.ValueChanged += NumericUpDownAlternatingInterval_ValueChanged;

            labelAlternatingSeconds = new Label
            {
                AutoSize = true,
                Text = "sec",
                Margin = Padding.Empty,
            };

            labelCycleMode = new Label
            {
                AutoSize = true,
                Text = ActivitiesUiText.CycleModeLabel,
                Cursor = Cursors.Default,
                Margin = Padding.Empty,
            };
            labelCycleMode.MouseEnter += CycleModeLabel_MouseEnter;
            labelCycleMode.MouseLeave += CycleModeLabel_MouseLeave;

            comboBoxCycleMode = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Margin = new Padding(0, 1, 0, 0),
            };
            comboBoxCycleMode.Items.AddRange(new object[]
            {
                ActivitiesUiText.CycleModeSlotSwap,
                ActivitiesUiText.CycleModePresetSwap,
            });
            // Size to the longest mode name so the closed combo isn't wider than needed.
            comboBoxCycleMode.Width = Math.Max(
                TextRenderer.MeasureText(ActivitiesUiText.CycleModePresetSwap, Font).Width,
                TextRenderer.MeasureText(ActivitiesUiText.CycleModeSlotSwap, Font).Width) +
                SystemInformation.VerticalScrollBarWidth + 10;
            comboBoxCycleMode.SelectedIndexChanged += ComboBoxCycleMode_SelectedIndexChanged;

            labelCyclingActiveBanner = new Label
            {
                AutoSize = true,
                Text = ActivitiesUiText.CyclingPresetsActiveBanner,
                Visible = false,
                Cursor = Cursors.Default,
                Margin = Padding.Empty,
                Font = new Font(Font, FontStyle.Bold),
            };
            labelCyclingActiveBanner.MouseEnter += CyclingActiveBanner_MouseEnter;
            labelCyclingActiveBanner.MouseLeave += CyclingActiveBanner_MouseLeave;

            alternatingPresetsTimer = new WinTimer();
            alternatingPresetsTimer.Tick += AlternatingPresetsTimer_Tick;

            Controls.Add(panelSeparator5);
            Controls.Add(panelSeparator6);
            Controls.Add(checkBoxAlternatingEnabled);
            Controls.Add(labelAlternatingPresets);
            Controls.Add(buttonAlternatingBrowse);
            Controls.Add(textBoxAlternatingFolder);
            Controls.Add(comboBoxAlternatingType);
            Controls.Add(labelAlternatingSwitchInterval);
            Controls.Add(numericUpDownAlternatingMinutes);
            Controls.Add(labelAlternatingMinutes);
            Controls.Add(numericUpDownAlternatingSeconds);
            Controls.Add(labelAlternatingSeconds);
            Controls.Add(labelCycleMode);
            Controls.Add(comboBoxCycleMode);

            panelSeparator5.BringToFront();
            panelSeparator6.BringToFront();
            checkBoxAlternatingEnabled.BringToFront();
            labelAlternatingPresets.BringToFront();

            LoadAlternatingPresetsUiFromSettings();
            ApplyAlternatingPresetsTheme();

            // Baseline snapshot for an already-enabled session is taken in InitializeSlotSystem
            // after LoadSelectedSlotToEditor (editor must not overwrite slots with empty defaults).
            SyncAlternatingPresetsTimer();
            UpdateCyclingEditLock();
        }

        void EnsureCyclingActiveBannerParented()
        {
            if (labelCyclingActiveBanner == null || panelActivities == null)
                return;

            if (labelCyclingActiveBanner.Parent != panelActivities)
                panelActivities.Controls.Add(labelCyclingActiveBanner);

            labelCyclingActiveBanner.BringToFront();
        }

        void LoadAlternatingPresetsUiFromSettings()
        {
            syncingAlternatingUi = true;
            try
            {
                textBoxAlternatingFolder.Text = FormatAlternatingFolderDisplay(settings.alternatingPresetsFolder);
                comboBoxAlternatingType.SelectedIndex = settings.alternatingPresetsUseCmrp ? 0 : 1;
                decimal minutes = Math.Max(0, Math.Min(1440, settings.alternatingPresetsIntervalMinutes));
                decimal seconds = Math.Max(0, Math.Min(59, settings.alternatingPresetsIntervalSeconds));
                if (minutes == 0 && seconds == 0)
                {
                    minutes = 5;
                    seconds = 0;
                }
                else if (minutes == 0 && seconds < 5)
                {
                    seconds = 5;
                }

                numericUpDownAlternatingSeconds.Minimum = minutes == 0 ? 5 : 0;
                numericUpDownAlternatingMinutes.Value = minutes;
                numericUpDownAlternatingSeconds.Value = Math.Max(numericUpDownAlternatingSeconds.Minimum, seconds);

                int mode = settings.cyclingPresetsMode == 0 ? 0 : 1;
                if (!settings.alternatingPresetsUseCmrp)
                    mode = 0;
                comboBoxCycleMode.SelectedIndex = mode;
                settings.cyclingPresetsMode = mode;

                if (settings.alternatingPresetsEnabled && !HasValidCycleFolder())
                {
                    settings.alternatingPresetsEnabled = false;
                    Utils.SaveSettings();
                }

                checkBoxAlternatingEnabled.Checked = settings.alternatingPresetsEnabled;
                SyncCycleModeAvailability();
                UpdateCycleEnableAvailability();
            }
            finally
            {
                syncingAlternatingUi = false;
            }
        }

        void ApplyAlternatingPresetsTheme()
        {
            if (checkBoxAlternatingEnabled == null)
                return;

            checkBoxAlternatingEnabled.ForeColor = CurrentColors.TextColor;

            if (labelAlternatingPresets != null)
                labelAlternatingPresets.ForeColor = CurrentColors.TextColor;

            if (labelAlternatingSwitchInterval != null)
                labelAlternatingSwitchInterval.ForeColor = CurrentColors.TextColor;

            if (labelAlternatingMinutes != null)
                labelAlternatingMinutes.ForeColor = CurrentColors.TextColor;

            if (labelAlternatingSeconds != null)
                labelAlternatingSeconds.ForeColor = CurrentColors.TextColor;

            if (labelCycleMode != null)
                labelCycleMode.ForeColor = CurrentColors.TextColor;

            UpdateCyclingStatusBanner();

            if (textBoxAlternatingFolder != null)
            {
                textBoxAlternatingFolder.BackColor = CurrentColors.BgTextFields;
                textBoxAlternatingFolder.ForeColor = CurrentColors.TextColor;
            }

            if (comboBoxAlternatingType != null)
            {
                comboBoxAlternatingType.BackColor = CurrentColors.BgTextFields;
                comboBoxAlternatingType.ForeColor = CurrentColors.TextColor;
            }

            if (comboBoxCycleMode != null)
            {
                comboBoxCycleMode.BackColor = CurrentColors.BgTextFields;
                comboBoxCycleMode.ForeColor = CurrentColors.TextColor;
            }

            if (numericUpDownAlternatingMinutes != null)
            {
                numericUpDownAlternatingMinutes.BackColor = CurrentColors.BgTextFields;
                numericUpDownAlternatingMinutes.ForeColor = CurrentColors.TextColor;
            }

            if (numericUpDownAlternatingSeconds != null)
            {
                numericUpDownAlternatingSeconds.BackColor = CurrentColors.BgTextFields;
                numericUpDownAlternatingSeconds.ForeColor = CurrentColors.TextColor;
            }

            if (buttonAlternatingBrowse != null)
                ThemeActionButton(buttonAlternatingBrowse);

            if (panelSeparator5 != null)
                panelSeparator5.BackColor = CurrentColors.TextColor;
            if (panelSeparator6 != null)
                panelSeparator6.BackColor = CurrentColors.TextColor;
        }

        void PlaceAlternatingPresetsSection(int blockLeft, ref int y)
        {
            if (panelSeparator5 == null || checkBoxAlternatingEnabled == null)
                return;

            PlaceSeparator(panelSeparator5, ref y);

            // Right inset for the Cycle RP's block (higher = shift whole section left).
            const int rightMargin = 11;
            const int gap = 6;
            int contentRight = Math.Max(rightMargin + 100, ClientSize.Width - rightMargin);

            int cycleModeWidth = comboBoxCycleMode.Width;
            int browseWidth = buttonAlternatingBrowse.PreferredSize.Width;
            int typeWidth = comboBoxAlternatingType.Width;
            int minutesWidth = numericUpDownAlternatingMinutes.Width;
            int secondsWidth = numericUpDownAlternatingSeconds.Width;
            int minLabelW = labelAlternatingMinutes.PreferredWidth;
            int secLabelW = labelAlternatingSeconds.PreferredWidth;

            // Pack from the right so Cycle Mode never touches/overflows the form edge.
            int cycleModeLeft = contentRight - cycleModeWidth;
            int secLabelLeft = cycleModeLeft - gap - secLabelW;
            int secondsLeft = secLabelLeft - 2 - secondsWidth;
            int minLabelLeft = secondsLeft - 3 - minLabelW;
            int minutesLeft = minLabelLeft - 2 - minutesWidth;

            int usedFromBrowseToType = browseWidth + gap + typeWidth + gap;
            int folderWidth = Math.Max(56, minutesLeft - gap - usedFromBrowseToType - LayoutContentLeft);
            folderWidth = Math.Min(100, folderWidth);

            int contentLeft = minutesLeft - gap - typeWidth - gap - folderWidth - gap - browseWidth;
            if (contentLeft < LayoutContentLeft)
            {
                int shift = LayoutContentLeft - contentLeft;
                contentLeft += shift;
                folderWidth = Math.Max(48, folderWidth - shift);
            }

            int titleTextY = y + Math.Max(0, (checkBoxAlternatingEnabled.Height - labelAlternatingPresets.PreferredHeight) / 2);
            checkBoxAlternatingEnabled.Location = new Point(contentLeft, y);
            labelAlternatingPresets.Location = new Point(checkBoxAlternatingEnabled.Right + 2, titleTextY);
            labelAlternatingSwitchInterval.Location = new Point(Math.Max(contentLeft, minutesLeft - 4), titleTextY);
            // Nudge label left so its left edge lines up with the combo's text/border (combo chrome).
            labelCycleMode.Location = new Point(cycleModeLeft - 3, titleTextY);

            y = Math.Max(
                Math.Max(checkBoxAlternatingEnabled.Bottom, labelAlternatingPresets.Bottom),
                Math.Max(labelAlternatingSwitchInterval.Bottom, labelCycleMode.Bottom)) + 6;

            int rowTop = y;
            buttonAlternatingBrowse.Location = new Point(contentLeft, rowTop);
            textBoxAlternatingFolder.Width = folderWidth;
            textBoxAlternatingFolder.Location = new Point(buttonAlternatingBrowse.Right + gap, rowTop + 1);
            comboBoxAlternatingType.Location = new Point(textBoxAlternatingFolder.Right + gap, rowTop + 1);

            numericUpDownAlternatingMinutes.Location = new Point(minutesLeft, rowTop + 1);
            int labelY = rowTop + 1 + Math.Max(0, (numericUpDownAlternatingMinutes.Height - labelAlternatingMinutes.PreferredHeight) / 2);
            labelAlternatingMinutes.Location = new Point(numericUpDownAlternatingMinutes.Right + 2, labelY);

            numericUpDownAlternatingSeconds.Location = new Point(labelAlternatingMinutes.Right + 3, rowTop + 1);
            labelAlternatingSeconds.Location = new Point(numericUpDownAlternatingSeconds.Right + 2, labelY);

            comboBoxCycleMode.Location = new Point(cycleModeLeft, rowTop + 1);

            // Final clamp if anything still overflows (DPI / font quirks).
            int overflow = comboBoxCycleMode.Right - contentRight;
            if (overflow > 0)
            {
                foreach (Control c in new Control[]
                {
                    checkBoxAlternatingEnabled, labelAlternatingPresets, labelAlternatingSwitchInterval, labelCycleMode,
                    buttonAlternatingBrowse, textBoxAlternatingFolder, comboBoxAlternatingType,
                    numericUpDownAlternatingMinutes, labelAlternatingMinutes,
                    numericUpDownAlternatingSeconds, labelAlternatingSeconds, comboBoxCycleMode,
                })
                {
                    if (c != null)
                        c.Left -= overflow;
                }
            }

            y = Math.Max(
                Math.Max(buttonAlternatingBrowse.Bottom, textBoxAlternatingFolder.Bottom),
                Math.Max(comboBoxAlternatingType.Bottom,
                    Math.Max(numericUpDownAlternatingSeconds.Bottom, comboBoxCycleMode.Bottom)));

            y += 10;
            panelSeparator6.Location = new Point(0, y);
            panelSeparator6.Height = 1;
            y += 1 + 2;
        }

        void AlternatingPresetsTitle_MouseEnter(object sender, EventArgs e)
        {
            if (labelAlternatingPresets == null || alternatingPresetsHoverPopup == null)
                return;

            alternatingPresetsHoverPopup.CancelHide();
            alternatingPresetsHoverPopup.ShowLines(
                ActivitiesInfoContent.GetCyclePresetsLines(),
                labelAlternatingPresets.PointToScreen(Point.Empty),
                RectangleToScreen(ClientRectangle),
                null);
        }

        void AlternatingPresetsTitle_MouseLeave(object sender, EventArgs e) =>
            alternatingPresetsHoverPopup?.ScheduleHide();

        void CycleModeLabel_MouseEnter(object sender, EventArgs e)
        {
            if (labelCycleMode == null || cycleModeHoverPopup == null)
                return;

            cycleModeHoverPopup.CancelHide();
            cycleModeHoverPopup.ShowLines(
                ActivitiesInfoContent.GetCycleModeLines(),
                labelCycleMode.PointToScreen(Point.Empty),
                RectangleToScreen(ClientRectangle),
                null);
        }

        void CycleModeLabel_MouseLeave(object sender, EventArgs e) =>
            cycleModeHoverPopup?.ScheduleHide();

        void SwitchIntervalLabel_MouseEnter(object sender, EventArgs e)
        {
            if (labelAlternatingSwitchInterval == null || switchIntervalHoverPopup == null)
                return;

            switchIntervalHoverPopup.CancelHide();
            switchIntervalHoverPopup.ShowLines(
                ActivitiesInfoContent.GetSwitchIntervalLines(),
                labelAlternatingSwitchInterval.PointToScreen(Point.Empty),
                RectangleToScreen(ClientRectangle),
                null);
        }

        void SwitchIntervalLabel_MouseLeave(object sender, EventArgs e) =>
            switchIntervalHoverPopup?.ScheduleHide();

        void CyclingActiveBanner_MouseEnter(object sender, EventArgs e)
        {
            if (labelCyclingActiveBanner == null || cyclingActiveBannerHoverPopup == null)
                return;

            cyclingActiveBannerHoverPopup.CancelHide();
            cyclingActiveBannerHoverPopup.ShowLines(
                IsCyclingPendingConnect()
                    ? ActivitiesInfoContent.GetCyclingPendingBannerLines()
                    : ActivitiesInfoContent.GetCyclingActiveBannerLines(),
                labelCyclingActiveBanner.PointToScreen(Point.Empty),
                RectangleToScreen(ClientRectangle),
                null);
        }

        void CyclingActiveBanner_MouseLeave(object sender, EventArgs e) =>
            cyclingActiveBannerHoverPopup?.ScheduleHide();

        void AlternatingPresetsTitle_Click(object sender, EventArgs e)
        {
            if (checkBoxAlternatingEnabled == null || !checkBoxAlternatingEnabled.Enabled)
                return;

            checkBoxAlternatingEnabled.Checked = !checkBoxAlternatingEnabled.Checked;
        }

        void ButtonAlternatingBrowse_Click(object sender, EventArgs e)
        {
            if (IsCyclingEditLocked())
                return;

            using (var dialog = new FolderBrowserDialog())
            {
                dialog.Description = "Choose a folder of presets to cycle through";
                if (!string.IsNullOrWhiteSpace(settings.alternatingPresetsFolder) &&
                    Directory.Exists(settings.alternatingPresetsFolder))
                    dialog.SelectedPath = settings.alternatingPresetsFolder;

                if (dialog.ShowDialog(this) != DialogResult.OK)
                    return;

                settings.alternatingPresetsFolder = dialog.SelectedPath;
                textBoxAlternatingFolder.Text = FormatAlternatingFolderDisplay(dialog.SelectedPath);
                ResetCycleProgress(cancelInFlight: true);
                Utils.SaveSettings();
                UpdateCycleEnableAvailability();
                SyncAlternatingPresetsTimer(restartIfRunning: true);
                UpdateActivePresetMenuLabels();
            }
        }

        static string FormatAlternatingFolderDisplay(string fullPath)
        {
            if (string.IsNullOrWhiteSpace(fullPath))
                return "";

            string trimmed = fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string name = Path.GetFileName(trimmed);
            if (string.IsNullOrEmpty(name))
                name = trimmed;

            return ".\\" + name + "\\";
        }

        void CheckBoxAlternatingEnabled_CheckedChanged(object sender, EventArgs e)
        {
            if (syncingAlternatingUi)
                return;

            if (checkBoxAlternatingEnabled.Checked && !HasValidCycleFolder())
            {
                syncingAlternatingUi = true;
                try
                {
                    checkBoxAlternatingEnabled.Checked = false;
                }
                finally
                {
                    syncingAlternatingUi = false;
                }

                QuietMessageBox.Show(
                    "Choose a valid Cycle RP's folder before enabling.",
                    ActivitiesUiText.CyclePresetsTitle,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                UpdateCycleEnableAvailability();
                return;
            }

            settings.alternatingPresetsEnabled = checkBoxAlternatingEnabled.Checked;
            Utils.SaveSettings();

            if (checkBoxAlternatingEnabled.Checked)
            {
                ClearPendingCycleSteps();
                cycleTransitionFromPath = null;
                activeCyclePresetPath = loadedPresetPath;
                SeedCycleIndexFromCurrentPreset();
                // Flush editor into the chart before arming the lock, then snapshot.
                FlushEditorToSelectedSlot(refreshList: false);
                CaptureCycleBaseline();
                ApplyMatchOrderSuppressionForCycleMode();
            }
            else
            {
                cancelCycleAfterCurrentStep = true;
                if (!alternatingApplyInProgress && !cyclePresetSwapInProgress)
                    ClearPendingCycleSteps();
                activeCyclePresetPath = null;
                cycleTransitionFromPath = null;
                RestoreCycleBaseline();
                RestoreMatchOrderAfterCycle();
            }

            UpdateCyclingEditLock();
            UpdateCycleEnableAvailability();
            SyncAlternatingPresetsTimer(restartIfRunning: true);
            UpdateActivePresetMenuLabels();
            MaybeAutoconnectForCycleSession();
        }

        bool HasValidCycleFolder() =>
            settings != null &&
            !string.IsNullOrWhiteSpace(settings.alternatingPresetsFolder) &&
            Directory.Exists(settings.alternatingPresetsFolder);

        void UpdateCycleEnableAvailability()
        {
            if (checkBoxAlternatingEnabled == null)
                return;

            // Always allow turning Cycle off; only block turning on without a folder.
            bool canToggleOn = HasValidCycleFolder();
            bool currentlyOn = checkBoxAlternatingEnabled.Checked;
            checkBoxAlternatingEnabled.Enabled = currentlyOn || canToggleOn;
        }

        /// <summary>
        /// Connect All when Settings → Autoconnect is on (used when enabling or disabling Cycle RP's).
        /// </summary>
        void MaybeAutoconnectForCycleSession()
        {
            if (settings == null || !settings.autoconnect)
                return;

            if (IsMultiSlotRpcMode())
            {
                if (!TryValidateEnabledSlotsForConnect(out _))
                    return;

                cycleAutoconnectInProgress = true;
                UpdateGlobalConnectionUi();
                _ = RunCycleSessionAutoconnectAsync();
                return;
            }

            var slot = GetSelectedSlot();
            if (slot == null || !slot.Enabled || string.IsNullOrWhiteSpace(slot.ApplicationId))
                return;

            if (!TryValidateSlotForConnect(slot, out _))
                return;

            slotService.ConnectSlot(slot);
        }

        async Task RunCycleSessionAutoconnectAsync()
        {
            try
            {
                await slotService.ConnectAllEnabledSlotsAsync(ShouldMatchDiscordListOrder(), GetMatchListOrderDelayMs);
            }
            finally
            {
                cycleAutoconnectInProgress = false;
                if (!IsDisposed && IsHandleCreated)
                    BeginInvoke(new MethodInvoker(UpdateGlobalConnectionUi));
            }
        }

        void ComboBoxAlternatingType_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (syncingAlternatingUi || IsCyclingEditLocked())
                return;

            settings.alternatingPresetsUseCmrp = comboBoxAlternatingType.SelectedIndex == 0;
            ResetCycleProgress(cancelInFlight: true);
            SyncCycleModeAvailability();
            Utils.SaveSettings();
            SyncAlternatingPresetsTimer(restartIfRunning: true);
        }

        void ComboBoxCycleMode_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (syncingAlternatingUi || IsCyclingEditLocked())
                return;

            int mode = comboBoxCycleMode.SelectedIndex == 1 ? 1 : 0;
            if (!settings.alternatingPresetsUseCmrp)
                mode = 0;

            settings.cyclingPresetsMode = mode;
            if (comboBoxCycleMode.SelectedIndex != mode)
            {
                syncingAlternatingUi = true;
                try
                {
                    comboBoxCycleMode.SelectedIndex = mode;
                }
                finally
                {
                    syncingAlternatingUi = false;
                }
            }

            ClearPendingCycleSteps();
            Utils.SaveSettings();
            RestoreMatchOrderAfterCycle();

            UpdateCyclingEditLock();
            UpdateActivePresetMenuLabels();
        }

        void SyncCycleModeAvailability()
        {
            if (comboBoxCycleMode == null)
                return;

            bool allowPresetSwap = settings.alternatingPresetsUseCmrp;
            if (!allowPresetSwap && (settings.cyclingPresetsMode != 0 || comboBoxCycleMode.SelectedIndex != 0))
            {
                settings.cyclingPresetsMode = 0;
                syncingAlternatingUi = true;
                try
                {
                    comboBoxCycleMode.SelectedIndex = 0;
                }
                finally
                {
                    syncingAlternatingUi = false;
                }
            }

            // .crp: Slot Swap only — disable Cycle Mode so Preset Swap cannot be chosen.
            // Also lock Cycle Mode while Cycle RP's is active.
            comboBoxCycleMode.Enabled = allowPresetSwap && !IsCyclingEditLocked();
        }

        void NumericUpDownAlternatingInterval_ValueChanged(object sender, EventArgs e)
        {
            if (syncingAlternatingUi)
                return;

            SyncAlternatingSecondsMinimum(numericUpDownAlternatingMinutes.Value);

            settings.alternatingPresetsIntervalMinutes = numericUpDownAlternatingMinutes.Value;
            settings.alternatingPresetsIntervalSeconds = numericUpDownAlternatingSeconds.Value;
            Utils.SaveSettings();
            SyncAlternatingPresetsTimer(restartIfRunning: true);
        }

        void SyncAlternatingSecondsMinimum(decimal minutes)
        {
            decimal minSeconds = minutes == 0 ? 5 : 0;
            if (numericUpDownAlternatingSeconds.Minimum == minSeconds &&
                numericUpDownAlternatingSeconds.Value >= minSeconds)
                return;

            bool wasSyncing = syncingAlternatingUi;
            syncingAlternatingUi = true;
            try
            {
                numericUpDownAlternatingSeconds.Minimum = minSeconds;
                if (numericUpDownAlternatingSeconds.Value < minSeconds)
                    numericUpDownAlternatingSeconds.Value = minSeconds;
            }
            finally
            {
                syncingAlternatingUi = wasSyncing;
            }
        }

        int GetAlternatingIntervalMs()
        {
            int minutes = (int)Math.Max(0, settings.alternatingPresetsIntervalMinutes);
            int seconds = (int)Math.Max(0, Math.Min(59, settings.alternatingPresetsIntervalSeconds));
            if (minutes == 0)
                seconds = Math.Max(5, seconds);
            int totalSeconds = minutes * 60 + seconds;
            return Math.Max(1, totalSeconds) * 1000;
        }

        bool AreAllEnabledSlotsConnected()
        {
            if (slotService == null)
                return false;

            // Legacy: only one activity can be connected — treat that as ready for cycling.
            if (!IsMultiSlotRpcMode())
            {
                return slotService.Slots.Any(s =>
                    !string.IsNullOrWhiteSpace(s.ApplicationId) && s.IsConnected);
            }

            var enabled = slotService.Slots
                .Where(s => s.Enabled && !string.IsNullOrWhiteSpace(s.ApplicationId))
                .ToList();

            if (enabled.Count == 0)
                return false;

            return enabled.All(s => s.IsConnected);
        }

        bool IsCyclingEditLocked() =>
            settings != null && settings.alternatingPresetsEnabled;

        /// <summary>
        /// Cycling is armed but waiting for every enabled slot to be connected before the timer runs.
        /// Mid-swap counts as active (not pending), even if some slots briefly disconnect.
        /// </summary>
        bool IsCyclingPendingConnect()
        {
            if (!IsCyclingEditLocked())
                return false;

            if (cyclePresetSwapInProgress || (pendingCycleSteps != null && pendingCycleSteps.Count > 0))
                return false;

            return !AreAllEnabledSlotsConnected();
        }

        // Lighter, less saturated blue than button hover (72,86,193).
        static readonly Color CyclingActiveBannerColor = Color.FromArgb(130, 155, 235);
        static readonly Color CyclingPendingBannerColor = Color.FromArgb(235, 215, 110);

        void UpdateCyclingStatusBanner()
        {
            if (labelCyclingActiveBanner == null)
                return;

            bool locked = IsCyclingEditLocked();
            EnsureCyclingActiveBannerParented();
            labelCyclingActiveBanner.Visible = locked;

            if (!locked)
                return;

            string text;
            Color color;
            if (IsCyclingPendingConnect())
            {
                text = IsMultiSlotRpcMode()
                    ? ActivitiesUiText.CyclingPresetsPendingBanner
                    : ActivitiesUiText.CyclingPresetsPendingBannerLegacy;
                color = CyclingPendingBannerColor;
            }
            else
            {
                text = ActivitiesUiText.CyclingPresetsActiveBanner;
                color = CyclingActiveBannerColor;
            }

            bool textChanged = !string.Equals(labelCyclingActiveBanner.Text, text, StringComparison.Ordinal);
            labelCyclingActiveBanner.Text = text;
            labelCyclingActiveBanner.ForeColor = color;

            if (textChanged && layoutInitialized)
                ApplyMainFormLayout();
        }

        bool IsPresetSwapMode() =>
            settings.alternatingPresetsUseCmrp && settings.cyclingPresetsMode == 1;

        void CaptureCycleBaseline()
        {
            if (slotService == null)
            {
                cycleBaselineSlots = null;
                cycleBaselineSelectedId = null;
                return;
            }

            // Snapshot chart slots only — never pull from the editor here.
            // (On startup the editor is still empty when Cycle RP's was left on.)
            cycleBaselineSlots = slotService.Slots.Select(SlotStorage.CloneForExport).ToList();
            cycleBaselineSelectedId = selectedSlotId;
        }

        void RestoreCycleBaseline()
        {
            if (slotService == null || cycleBaselineSlots == null)
                return;

            DisconnectAllSlots();
            slotService.Slots.Clear();
            foreach (var slot in cycleBaselineSlots)
            {
                slot.ResetRuntimeState();
                slotService.Slots.Add(slot);
            }

            selectedSlotId = cycleBaselineSelectedId;
            if (string.IsNullOrEmpty(selectedSlotId) || slotService.GetSlot(selectedSlotId) == null)
                selectedSlotId = slotService.Slots.Count > 0 ? slotService.Slots[0].SlotId : null;

            LoadSelectedSlotToEditor();
            RefreshSlotListView();
            RestoreListSelection(keepEditorFocus: true, ensureVisible: true);
            UpdateSlotActionButtons();
            UpdateGlobalConnectionUi();
            SaveSlotsToStorage();
            cycleBaselineSlots = null;
            cycleBaselineSelectedId = null;
        }

        void ApplyMatchOrderSuppressionForCycleMode()
        {
            // Match Order applies to both Slot Swap and Preset Swap — no suppression.
        }

        void RestoreMatchOrderAfterCycle()
        {
            // Intentionally empty: Match Order is no longer forced off while cycling.
        }

        /// <summary>
        /// Turns Cycle RP's off, restores the pre-cycle chart, and stops in-flight swaps.
        /// </summary>
        void CancelCyclePresetsSession(bool restoreBaseline = true)
        {
            cancelCycleAfterCurrentStep = true;
            ClearPendingCycleSteps();
            cyclePresetSwapInProgress = false;
            alternatingApplyInProgress = false;

            if (checkBoxAlternatingEnabled != null && checkBoxAlternatingEnabled.Checked)
            {
                syncingAlternatingUi = true;
                try
                {
                    checkBoxAlternatingEnabled.Checked = false;
                }
                finally
                {
                    syncingAlternatingUi = false;
                }
            }

            settings.alternatingPresetsEnabled = false;
            Utils.SaveSettings();
            activeCyclePresetPath = null;
            cycleTransitionFromPath = null;

            if (restoreBaseline)
                RestoreCycleBaseline();
            else
            {
                cycleBaselineSlots = null;
                cycleBaselineSelectedId = null;
            }

            RestoreMatchOrderAfterCycle();
            UpdateCyclingEditLock();
            SyncAlternatingPresetsTimer();
            UpdateActivePresetMenuLabels();
        }

        void UpdateCyclingEditLock()
        {
            bool locked = IsCyclingEditLocked();

            if (slotService != null)
                slotService.SuppressPresenceErrorUi = locked;

            UpdateCyclingStatusBanner();

            SetPresenceEditorEnabled(!locked);

            if (buttonAddSlot != null)
                UpdateSlotActionButtons();

            if (slotService != null)
                UpdateGlobalConnectionUi();

            if (listViewSlots != null)
                listViewSlots.AllowDrop = !locked && IsMultiSlotRpcMode();

            if (checkBoxMatchDiscordListOrder != null)
            {
                checkBoxMatchDiscordListOrder.Enabled = !locked && IsMultiSlotRpcMode();
                checkBoxMatchDiscordListOrder.ForeColor = checkBoxMatchDiscordListOrder.Enabled
                    ? CurrentColors.TextColor
                    : CurrentColors.TextInactive;
            }

            // Keep labels Enabled so we can force TextInactive (WinForms disabled labels use system gray/black).
            if (labelMatchOrderText != null)
            {
                labelMatchOrderText.Enabled = true;
                labelMatchOrderText.ForeColor = (!locked && IsMultiSlotRpcMode())
                    ? CurrentColors.TextColor
                    : CurrentColors.TextInactive;
            }

            UpdateMatchOrderDelayControls();

            // Lock folder/format/mode while cycling — interval stays editable.
            if (buttonAlternatingBrowse != null)
                buttonAlternatingBrowse.Enabled = !locked;
            if (textBoxAlternatingFolder != null)
                textBoxAlternatingFolder.Enabled = !locked;
            if (comboBoxAlternatingType != null)
                comboBoxAlternatingType.Enabled = !locked;

            SyncCycleModeAvailability();

            UpdateCycleEnableAvailability();

            SyncActivitiesListScrollbar();
            UpdatePresetFileMenuItems();

            if (layoutInitialized)
                ApplyMainFormLayout();

            UpdateActivePresetMenuLabels();
        }

        void SetPresenceEditorEnabled(bool enabled)
        {
            bool wasLoading = loading;
            loading = true;
            try
            {
                foreach (Control control in new Control[]
                {
                    textBoxID, textBoxName, textBoxDetails, textBoxDetailsURL, textBoxState, textBoxStateURL,
                    textBoxLargeText, textBoxLargeURL, textBoxSmallText, textBoxSmallURL,
                    textBoxButton1Text, textBoxButton1URL, textBoxButton2Text, textBoxButton2URL,
                    comboBoxLargeKey, comboBoxSmallKey, comboBoxType, comboBoxDisplay,
                    numericUpDownPartySize, numericUpDownPartyMax,
                    dateTimePickerTimestampStart, dateTimePickerTimestampEnd, checkBoxTimestampEnd,
                    radioButtonLastConnection, radioButtonStartTime, radioButtonPresence, radioButtonLocalTime, radioButtonCustom,
                })
                {
                    if (control != null)
                        control.Enabled = enabled;
                }

                if (!enabled && textBoxID != null)
                    textBoxID.ReadOnly = true;
            }
            finally
            {
                loading = wasLoading;
            }
        }

        void SyncAlternatingPresetsTimer(bool restartIfRunning = false)
        {
            if (alternatingPresetsTimer == null)
                return;

            bool allConnected = AreAllEnabledSlotsConnected();
            bool shouldRun = settings.alternatingPresetsEnabled &&
                !string.IsNullOrWhiteSpace(settings.alternatingPresetsFolder) &&
                Directory.Exists(settings.alternatingPresetsFolder) &&
                (cyclePresetSwapInProgress ||
                 (pendingCycleSteps != null && pendingCycleSteps.Count > 0) ||
                 allConnected);

            if (!shouldRun)
            {
                if (alternatingPresetsTimer.Enabled)
                alternatingPresetsTimer.Stop();
                return;
            }

            // Don't start a new countdown while a preset-swap rush is running.
            if (cyclePresetSwapInProgress)
                return;

            int intervalMs = GetAlternatingIntervalMs();

            if (alternatingPresetsTimer.Enabled && !restartIfRunning)
                return;

            alternatingPresetsTimer.Stop();
            alternatingPresetsTimer.Interval = intervalMs;
            alternatingPresetsTimer.Start();
        }

        void AlternatingPresetsTimer_Tick(object sender, EventArgs e)
        {
            if (cyclePresetSwapInProgress || alternatingApplyInProgress)
                return;

            ApplyNextAlternatingPreset();
        }

        void ApplyNextAlternatingPreset()
        {
            if (alternatingApplyInProgress || loading)
                return;

            if (!settings.alternatingPresetsEnabled)
            {
                ClearPendingCycleSteps();
                return;
            }

            // Slot Swap mid-target: continue same target across ticks until done.
            if (pendingCycleSteps != null && pendingCycleSteps.Count > 0)
            {
                _ = ExecutePendingSlotSwapStepAsync();
                return;
            }

            string folder = settings.alternatingPresetsFolder;
            if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
                return;

            string extension = settings.alternatingPresetsUseCmrp ? MultiSlotPresetExtension : ".crp";
            string[] files;
            try
            {
                files = Directory.GetFiles(folder, "*" + extension)
                    .OrderBy(f => Path.GetFileName(f), StringComparer.OrdinalIgnoreCase)
                    .ToArray();
            }
            catch
            {
                return;
            }

            if (files.Length == 0)
            {
                StopAlternatingPresetsTimer();
                return;
            }

            if (alternatingPresetIndex >= files.Length)
                alternatingPresetIndex = 0;

            string path = files[alternatingPresetIndex];
            var targets = TryLoadCycleTargetSlots(path);
            if (targets == null || targets.Count == 0)
            {
                alternatingPresetIndex = (alternatingPresetIndex + 1) % files.Length;
                return;
            }

            var steps = BuildCycleSwapSteps(
                slotService.Slots.ToList(),
                targets,
                pairByApplicationId: !settings.matchDiscordListOrder,
                applyMatchOrder: settings.matchDiscordListOrder);
            if (steps.Count == 0)
            {
                alternatingPresetIndex = (alternatingPresetIndex + 1) % files.Length;
                return;
            }

            pendingCycleTargetPath = path;
            cycleTransitionFromPath = !string.IsNullOrWhiteSpace(activeCyclePresetPath)
                ? activeCyclePresetPath
                : loadedPresetPath;
            UpdateActivePresetMenuLabels();
            pendingCycleSteps = steps;
            cancelCycleAfterCurrentStep = false;

            // Preset Swap rushes the full target. Slot Swap always does one step per switch interval
            // (Match Order only reorders steps: Playing/Competing first, then Listening/Watching).
            if (IsPresetSwapMode())
                _ = ExecuteCycleTargetRushAsync();
            else
                _ = ExecutePendingSlotSwapStepAsync();
        }

        List<PresenceSlot> TryLoadCycleTargetSlots(string path)
        {
            try
            {
                if (IsMultiSlotPresetFile(path))
                {
                    var serializer = new System.Web.Script.Serialization.JavaScriptSerializer { MaxJsonLength = int.MaxValue };
                    var preset = serializer.Deserialize<MultiSlotPreset>(File.ReadAllText(path));
                    if (preset?.Slots == null || preset.Slots.Length == 0)
                        return null;

                    var list = new List<PresenceSlot>();
                    int index = 0;
                    foreach (var stored in preset.Slots)
                    {
                        var slot = stored.ToPresenceSlot();
                        slot.SlotId = Guid.NewGuid().ToString("N");
                        if (string.IsNullOrWhiteSpace(slot.Label))
                            slot.Label = string.IsNullOrWhiteSpace(slot.Name) ? $"Activity {++index}" : slot.Name;
                        list.Add(slot);
                    }

                    return list;
                }

                using (var file = File.OpenRead(path))
                {
                    var xs = new XmlSerializer(typeof(Preset));
                    var preset = (Preset)xs.Deserialize(file);
                    var slot = PresenceSlot.FromPreset(preset, (int)settings.pipe);
                    slot.SlotId = Guid.NewGuid().ToString("N");
                    if (string.IsNullOrWhiteSpace(slot.Label))
                        slot.Label = string.IsNullOrWhiteSpace(slot.Name) ? "Activity 1" : slot.Name;
                    return new List<PresenceSlot> { slot };
                }
            }
            catch
            {
                return null;
            }
        }

        static List<CycleSwapStep> BuildCycleSwapSteps(
            List<PresenceSlot> current,
            List<PresenceSlot> targets,
            bool pairByApplicationId,
            bool applyMatchOrder)
        {
            if (current == null)
                current = new List<PresenceSlot>();
            if (targets == null)
                targets = new List<PresenceSlot>();

            var steps = pairByApplicationId
                ? BuildCycleSwapStepsByApplicationId(current, targets)
                : BuildCycleSwapStepsByIndex(current, targets);

            if (applyMatchOrder)
                return OrderCycleStepsForMatchOrder(steps);

            return steps;
        }

        static List<CycleSwapStep> BuildCycleSwapStepsByIndex(
            List<PresenceSlot> current,
            List<PresenceSlot> targets)
        {
            var steps = new List<CycleSwapStep>();
            int count = Math.Max(current.Count, targets.Count);
            for (int i = 0; i < count; i++)
            {
                var cur = i < current.Count ? current[i] : null;
                var tgt = i < targets.Count ? targets[i] : null;

                if (cur != null && tgt != null && CyclePresenceContentsEqual(cur, tgt))
                    continue;

                steps.Add(new CycleSwapStep
                {
                    Incoming = tgt,
                    OutgoingSlotId = cur?.SlotId,
                });
            }

            return steps;
        }

        /// <summary>
        /// Pair by matching Application ID first; leftover slots fall back to chart order.
        /// </summary>
        static List<CycleSwapStep> BuildCycleSwapStepsByApplicationId(
            List<PresenceSlot> current,
            List<PresenceSlot> targets)
        {
            var steps = new List<CycleSwapStep>();
            var usedCurrent = new HashSet<string>(StringComparer.Ordinal);
            var usedTargets = new HashSet<int>();

            for (int ti = 0; ti < targets.Count; ti++)
            {
                var tgt = targets[ti];
                string targetId = NormalizeCycleApplicationId(tgt?.ApplicationId);
                if (string.IsNullOrEmpty(targetId))
                    continue;

                PresenceSlot match = null;
                foreach (var cur in current)
                {
                    if (cur == null || usedCurrent.Contains(cur.SlotId))
                        continue;
                    if (!string.Equals(
                            NormalizeCycleApplicationId(cur.ApplicationId),
                            targetId,
                            StringComparison.OrdinalIgnoreCase))
                        continue;

                    match = cur;
                    break;
                }

                if (match == null)
                    continue;

                usedCurrent.Add(match.SlotId);
                usedTargets.Add(ti);

                if (CyclePresenceContentsEqual(match, tgt))
                    continue;

                steps.Add(new CycleSwapStep
                {
                    Incoming = tgt,
                    OutgoingSlotId = match.SlotId,
                });
            }

            var unusedCurrent = current
                .Where(c => c != null && !usedCurrent.Contains(c.SlotId))
                .ToList();
            int unusedIdx = 0;

            for (int ti = 0; ti < targets.Count; ti++)
            {
                if (usedTargets.Contains(ti))
                    continue;

                var tgt = targets[ti];
                PresenceSlot cur = unusedIdx < unusedCurrent.Count ? unusedCurrent[unusedIdx++] : null;
                if (cur != null)
                    usedCurrent.Add(cur.SlotId);

                if (cur != null && tgt != null && CyclePresenceContentsEqual(cur, tgt))
                    continue;

                steps.Add(new CycleSwapStep
                {
                    Incoming = tgt,
                    OutgoingSlotId = cur?.SlotId,
                });
            }

            while (unusedIdx < unusedCurrent.Count)
            {
                var cur = unusedCurrent[unusedIdx++];
                steps.Add(new CycleSwapStep
                {
                    Incoming = null,
                    OutgoingSlotId = cur.SlotId,
                });
            }

            return steps;
        }

        static string NormalizeCycleApplicationId(string applicationId) =>
            string.IsNullOrWhiteSpace(applicationId) ? "" : applicationId.Trim();

        static List<CycleSwapStep> OrderCycleStepsForMatchOrder(List<CycleSwapStep> steps)
        {
            var nonListeningWatching = new List<CycleSwapStep>();
            var listeningWatching = new List<CycleSwapStep>();
            var disconnectOnly = new List<CycleSwapStep>();

            foreach (var step in steps)
            {
                if (step.Incoming == null)
                {
                    disconnectOnly.Add(step);
                    continue;
                }

                if (IsListeningOrWatchingActivity(step.Incoming))
                    listeningWatching.Add(step);
                else
                    nonListeningWatching.Add(step);
            }

            listeningWatching.Reverse();

            var ordered = new List<CycleSwapStep>(steps.Count);
            ordered.AddRange(nonListeningWatching);
            ordered.AddRange(listeningWatching);
            ordered.AddRange(disconnectOnly);
            return ordered;
        }

        static bool IsListeningOrWatchingActivity(PresenceSlot slot) =>
            slot != null &&
            (slot.ActivityType == ActivityType.Listening || slot.ActivityType == ActivityType.Watching);

        static bool CyclePresenceContentsEqual(PresenceSlot a, PresenceSlot b)
        {
            if (a == null || b == null)
                return false;

            return a.Enabled == b.Enabled &&
                string.Equals(a.ApplicationId?.Trim(), b.ApplicationId?.Trim(), StringComparison.Ordinal) &&
                a.Type == b.Type &&
                a.Display == b.Display &&
                string.Equals(a.Name ?? "", b.Name ?? "", StringComparison.Ordinal) &&
                string.Equals(a.Details ?? "", b.Details ?? "", StringComparison.Ordinal) &&
                string.Equals(a.DetailsUrl ?? "", b.DetailsUrl ?? "", StringComparison.Ordinal) &&
                string.Equals(a.State ?? "", b.State ?? "", StringComparison.Ordinal) &&
                string.Equals(a.StateUrl ?? "", b.StateUrl ?? "", StringComparison.Ordinal) &&
                a.PartySize == b.PartySize &&
                a.PartyMax == b.PartyMax &&
                a.Timestamps == b.Timestamps &&
                a.CustomTimestamp == b.CustomTimestamp &&
                a.CustomTimestampEndEnabled == b.CustomTimestampEndEnabled &&
                a.CustomTimestampEnd == b.CustomTimestampEnd &&
                string.Equals(a.LargeImageKey ?? "", b.LargeImageKey ?? "", StringComparison.Ordinal) &&
                string.Equals(a.LargeImageText ?? "", b.LargeImageText ?? "", StringComparison.Ordinal) &&
                string.Equals(a.LargeImageUrl ?? "", b.LargeImageUrl ?? "", StringComparison.Ordinal) &&
                string.Equals(a.SmallImageKey ?? "", b.SmallImageKey ?? "", StringComparison.Ordinal) &&
                string.Equals(a.SmallImageText ?? "", b.SmallImageText ?? "", StringComparison.Ordinal) &&
                string.Equals(a.SmallImageUrl ?? "", b.SmallImageUrl ?? "", StringComparison.Ordinal) &&
                string.Equals(a.Button1Text ?? "", b.Button1Text ?? "", StringComparison.Ordinal) &&
                string.Equals(a.Button1Url ?? "", b.Button1Url ?? "", StringComparison.Ordinal) &&
                string.Equals(a.Button2Text ?? "", b.Button2Text ?? "", StringComparison.Ordinal) &&
                string.Equals(a.Button2Url ?? "", b.Button2Url ?? "", StringComparison.Ordinal);
        }

        static bool NeedsCycleDisconnectFirst(PresenceSlot outgoing, PresenceSlot incoming)
        {
            if (outgoing == null || incoming == null)
                return false;

            // Same App ID → update in place (never disconnect-first for ID clash).
            string outId = NormalizeCycleApplicationId(outgoing.ApplicationId);
            string inId = NormalizeCycleApplicationId(incoming.ApplicationId);
            if (!string.IsNullOrEmpty(outId) &&
                string.Equals(outId, inId, StringComparison.OrdinalIgnoreCase))
                return false;

            // Keep Playing clash handling: don't leave two Playings live at once.
            return outgoing.ActivityType == ActivityType.Playing &&
                incoming.ActivityType == ActivityType.Playing;
        }

        static bool CanCycleUpdateInPlace(PresenceSlot outgoing, PresenceSlot incoming)
        {
            if (outgoing == null || incoming == null)
                return false;

            string outId = NormalizeCycleApplicationId(outgoing.ApplicationId);
            string inId = NormalizeCycleApplicationId(incoming.ApplicationId);
            if (string.IsNullOrEmpty(outId) ||
                !string.Equals(outId, inId, StringComparison.OrdinalIgnoreCase))
                return false;

            return outgoing.IsConnected ||
                outgoing.ConnectionState == SlotConnectionState.Connected ||
                outgoing.ConnectionState == SlotConnectionState.UpdatingPresence;
        }

        async Task ExecutePendingSlotSwapStepAsync()
        {
            if (alternatingApplyInProgress || pendingCycleSteps == null || pendingCycleSteps.Count == 0)
                return;

            alternatingApplyInProgress = true;
            try
            {
                var step = pendingCycleSteps[0];
                pendingCycleSteps.RemoveAt(0);
                await ExecuteCycleSwapStepAsync(step);

                if (!settings.alternatingPresetsEnabled || cancelCycleAfterCurrentStep)
                {
                    ClearPendingCycleSteps();
                    cycleTransitionFromPath = null;
                    cancelCycleAfterCurrentStep = false;
                    UpdateCyclingEditLock();
                    UpdateActivePresetMenuLabels();
                    return;
                }

                if (pendingCycleSteps.Count == 0)
                {
                    CompleteCycleTargetTransition();
                }
                else
                {
                    UpdateActivePresetMenuLabels();
                }
            }
            finally
            {
                alternatingApplyInProgress = false;
                SyncAlternatingPresetsTimer(restartIfRunning: true);
            }
        }

        async Task ExecuteCycleTargetRushAsync()
        {
            if (alternatingApplyInProgress || pendingCycleSteps == null || pendingCycleSteps.Count == 0)
                return;

            alternatingApplyInProgress = true;
            cyclePresetSwapInProgress = true;
            alternatingPresetsTimer?.Stop();
            UpdateActivePresetMenuLabels();

            try
            {
                while (pendingCycleSteps.Count > 0)
                {
                    if (!settings.alternatingPresetsEnabled || cancelCycleAfterCurrentStep)
                        break;

                    var step = pendingCycleSteps[0];
                    pendingCycleSteps.RemoveAt(0);
                    await ExecuteCycleSwapStepAsync(step);

                    if (pendingCycleSteps.Count == 0)
                        break;

                    if (!settings.alternatingPresetsEnabled || cancelCycleAfterCurrentStep)
                        break;

                    // Playing/Competing (and non-L/W): 3s. Listening/Watching + Match Order: Match Order delay.
                    int delayMs = GetCycleInterStepDelayMs(pendingCycleSteps[0]);
                    await Task.Delay(delayMs);
                }

                if (settings.alternatingPresetsEnabled && !cancelCycleAfterCurrentStep &&
                    (pendingCycleSteps == null || pendingCycleSteps.Count == 0))
                {
                    CompleteCycleTargetTransition();
                }
                else
                {
                    ClearPendingCycleSteps();
                    cycleTransitionFromPath = null;
                    cancelCycleAfterCurrentStep = false;
                    UpdateActivePresetMenuLabels();
                }
            }
            finally
            {
                alternatingApplyInProgress = false;
                cyclePresetSwapInProgress = false;
                UpdateCyclingEditLock();
                SyncAlternatingPresetsTimer(restartIfRunning: true);
                UpdateActivePresetMenuLabels();
            }
        }

        /// <summary>
        /// Gap before the next rushed cycle step. Playing/Competing (and anything non-L/W) use 3s;
        /// Listening/Watching use Match Order delay when Match Order is enabled.
        /// </summary>
        int GetCycleInterStepDelayMs(CycleSwapStep nextStep)
        {
            if (settings != null &&
                settings.matchDiscordListOrder &&
                nextStep?.Incoming != null &&
                IsListeningOrWatchingActivity(nextStep.Incoming))
            {
                return GetMatchListOrderDelayMs();
            }

            return CyclePresetSwapStepDelayMs;
        }

        void CompleteCycleTargetTransition()
        {
            if (!string.IsNullOrWhiteSpace(pendingCycleTargetPath))
                activeCyclePresetPath = pendingCycleTargetPath;

            AdvanceCycleFileIndexAfterTargetComplete();
            ClearPendingCycleSteps();
            cycleTransitionFromPath = null;
            UpdateActivePresetMenuLabels();
        }

        string[] GetCyclePresetFiles()
        {
            string folder = settings?.alternatingPresetsFolder;
            if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
                return Array.Empty<string>();

            string extension = settings.alternatingPresetsUseCmrp ? MultiSlotPresetExtension : ".crp";
            try
            {
                return Directory.GetFiles(folder, "*" + extension)
                    .OrderBy(f => Path.GetFileName(f), StringComparer.OrdinalIgnoreCase)
                    .ToArray();
            }
            catch
            {
                return Array.Empty<string>();
            }
        }

        string GetUpcomingCycleTargetPath()
        {
            var files = GetCyclePresetFiles();
            if (files.Length == 0)
                return null;

            int index = alternatingPresetIndex;
            if (index < 0 || index >= files.Length)
                index = 0;

            return files[index];
        }

        /// <summary>
        /// Start at the file after the currently loaded/active preset (A5 → A6), wrapping A–Z.
        /// </summary>
        void SeedCycleIndexFromCurrentPreset()
        {
            var files = GetCyclePresetFiles();
            if (files.Length == 0)
            {
                alternatingPresetIndex = 0;
                return;
            }

            string current = !string.IsNullOrWhiteSpace(activeCyclePresetPath)
                ? activeCyclePresetPath
                : loadedPresetPath;

            if (string.IsNullOrWhiteSpace(current))
            {
                alternatingPresetIndex = 0;
                return;
            }

            string currentName = Path.GetFileName(current);
            int idx = Array.FindIndex(files, f =>
                string.Equals(f, current, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(Path.GetFileName(f), currentName, StringComparison.OrdinalIgnoreCase));

            alternatingPresetIndex = idx >= 0 ? (idx + 1) % files.Length : 0;
        }

        bool IsCycleTransitionInProgress() =>
            cyclePresetSwapInProgress ||
            (pendingCycleSteps != null && pendingCycleSteps.Count > 0) ||
            !string.IsNullOrWhiteSpace(pendingCycleTargetPath);

        /// <summary>
        /// Menu status name for Active Cycle Preset (paren filename, or from -> to while transitioning).
        /// Slot Swap always uses the arrow form; Preset Swap only during a morph.
        /// </summary>
        string BuildActiveCyclePresetMenuName()
        {
            string settled = !string.IsNullOrWhiteSpace(activeCyclePresetPath)
                ? activeCyclePresetPath
                : loadedPresetPath;

            bool slotSwap = !IsPresetSwapMode();
            bool transitioning = IsCycleTransitionInProgress();

            if (slotSwap || transitioning)
            {
                string fromPath = !string.IsNullOrWhiteSpace(cycleTransitionFromPath)
                    ? cycleTransitionFromPath
                    : settled;
                string toPath = !string.IsNullOrWhiteSpace(pendingCycleTargetPath)
                    ? pendingCycleTargetPath
                    : GetUpcomingCycleTargetPath();

                return FormatCyclePresetArrowName(fromPath, toPath);
            }

            return FormatCyclePresetParenName(settled);
        }

        static string FormatCyclePresetParenName(string path) =>
            string.IsNullOrWhiteSpace(path) ? "(no preset loaded)" : "(" + Path.GetFileName(path) + ")";

        static string FormatCyclePresetArrowName(string fromPath, string toPath) =>
            FormatCyclePresetParenName(fromPath) + " --> " + FormatCyclePresetParenName(toPath);

        async Task ExecuteCycleSwapStepAsync(CycleSwapStep step)
        {
            if (step == null || slotService == null)
                return;

            SaveEditorToSelectedSlot(refreshList: false);

            PresenceSlot outgoing = null;
            if (!string.IsNullOrEmpty(step.OutgoingSlotId))
                outgoing = slotService.GetSlot(step.OutgoingSlotId);

            PresenceSlot incoming = step.Incoming;

            // Same App ID on a live connection: SetPresence only (no reconnect / no second slot).
            if (CanCycleUpdateInPlace(outgoing, incoming))
            {
                outgoing.ApplyPresenceFieldsFrom(incoming);
                if (!outgoing.Enabled)
                    outgoing.Enabled = true;
                selectedSlotId = outgoing.SlotId;
                RefreshCycleChartUi();
                slotService.SetPresenceForSlot(outgoing, showErrors: false, ignoreThrottle: true);
                RefreshCycleChartUi();
                SaveSlotsToStorage();
                return;
            }


            if (incoming != null)
            {
                int insertAt = outgoing != null
                    ? Math.Max(0, slotService.Slots.IndexOf(outgoing))
                    : slotService.Slots.Count;
                if (insertAt < 0)
                    insertAt = slotService.Slots.Count;

                if (string.IsNullOrWhiteSpace(incoming.SlotId))
                    incoming.SlotId = Guid.NewGuid().ToString("N");

                slotService.Slots.Insert(insertAt, incoming);
                selectedSlotId = incoming.SlotId;
                RefreshCycleChartUi();
            }

            bool clashFirst = NeedsCycleDisconnectFirst(outgoing, incoming);

            if (clashFirst && outgoing != null)
            {
                await DisconnectAndRemoveCycleSlotAsync(outgoing);
                outgoing = null;
            }

            if (incoming != null)
            {
                bool connected = await ConnectCycleSlotAndWaitAsync(incoming);
                if (!connected)
                {
                    // Skip pair: leave Error status, do not remove outgoing.
                    RefreshCycleChartUi();
                    return;
                }
            }

            if (outgoing != null)
                await DisconnectAndRemoveCycleSlotAsync(outgoing);

            RefreshCycleChartUi();
            SaveSlotsToStorage();
        }

        async Task<bool> ConnectCycleSlotAndWaitAsync(PresenceSlot slot)
        {
            if (slot == null)
                return false;

            if (!slot.Enabled)
                slot.Enabled = true;

            // Cycle must never raise editing-mode restriction / build popups.
            slotService.ConnectSlot(slot, showErrors: false);
            RefreshCycleChartUi();

            var deadline = DateTime.UtcNow.AddMilliseconds(CycleConnectWaitTimeoutMs);
            while (DateTime.UtcNow < deadline)
            {
                if (slot.IsConnected || slot.ConnectionState == SlotConnectionState.Connected)
                    return true;

                if (slot.ConnectionState == SlotConnectionState.Error)
                    return false;

                await Task.Delay(50);
            }

            if (slot.IsConnected || slot.ConnectionState == SlotConnectionState.Connected)
                return true;

            slot.ConnectionState = SlotConnectionState.Error;
            if (string.IsNullOrWhiteSpace(slot.LastError))
                slot.LastError = "Timed out while connecting.";
            RefreshCycleChartUi();
            return false;
        }

        async Task DisconnectAndRemoveCycleSlotAsync(PresenceSlot slot)
        {
            if (slot == null || slotService == null)
                return;

            string slotId = slot.SlotId;
            slotService.DisconnectSlot(slot);
            slotService.Slots.Remove(slot);

            if (selectedSlotId == slotId)
            {
                selectedSlotId = slotService.Slots.Count > 0
                    ? slotService.Slots[Math.Min(slotService.Slots.Count - 1, 0)].SlotId
                    : null;
            }

            RefreshCycleChartUi();
            await Task.Delay(50);
        }

        void RefreshCycleChartUi()
        {
            LoadSelectedSlotToEditor();
            RefreshSlotListView();
            RestoreListSelection(keepEditorFocus: true, ensureVisible: false);
            UpdateSlotActionButtons();
            UpdateGlobalConnectionUi();
        }

        void AdvanceCycleFileIndexAfterTargetComplete()
        {
            string folder = settings.alternatingPresetsFolder;
            if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
            {
                alternatingPresetIndex = 0;
                return;
            }

            string extension = settings.alternatingPresetsUseCmrp ? MultiSlotPresetExtension : ".crp";
            try
            {
                var files = Directory.GetFiles(folder, "*" + extension)
                    .OrderBy(f => Path.GetFileName(f), StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                if (files.Length == 0)
                {
                    alternatingPresetIndex = 0;
                    return;
                }

                if (!string.IsNullOrEmpty(pendingCycleTargetPath))
                {
                    int idx = Array.FindIndex(files, f =>
                        string.Equals(f, pendingCycleTargetPath, StringComparison.OrdinalIgnoreCase));
                    alternatingPresetIndex = idx >= 0 ? (idx + 1) % files.Length : 0;
                }
                else
                {
                    alternatingPresetIndex = (alternatingPresetIndex + 1) % files.Length;
                }
            }
            catch
            {
                alternatingPresetIndex = 0;
            }
        }

        void ClearPendingCycleSteps()
        {
            pendingCycleSteps = null;
            pendingCycleTargetPath = null;
        }

        void ResetCycleProgress(bool cancelInFlight)
        {
            alternatingPresetIndex = 0;
            if (cancelInFlight)
                cancelCycleAfterCurrentStep = true;

            if (!alternatingApplyInProgress && !cyclePresetSwapInProgress)
            {
                cancelCycleAfterCurrentStep = false;
                ClearPendingCycleSteps();
            }
        }

        void StopAlternatingPresetsTimer()
        {
            alternatingPresetsTimer?.Stop();
        }
    }
}
