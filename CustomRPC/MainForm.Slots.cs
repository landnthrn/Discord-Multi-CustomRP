using DiscordRPC;
using Microsoft.AppCenter.Analytics;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using WinButton = System.Windows.Forms.Button;

namespace CustomRPC
{
    public partial class MainForm
    {
        PresenceSlotService slotService;
        Panel panelActivities;
        ListView listViewSlots;
        FlowLayoutPanel flowLayoutPanelSlotActions;
        FlowLayoutPanel flowLayoutPanelMatchOrder;
        Panel panelMatchOrderCheckBoxHost;
        WinButton buttonAddSlot;
        WinButton buttonDuplicateSlot;
        WinButton buttonRemoveSlot;
        WinButton buttonToggleEnabled;
        CheckBox checkBoxMatchDiscordListOrder;
        Label labelMatchOrderText;
        Label labelMatchOrderDelay;
        NumericUpDown numericUpDownMatchOrderDelay;
        bool syncingMatchListOrderToggle;
        bool syncingMatchListOrderDelay;
        WinButton buttonConnectAll;
        WinButton buttonDisconnectAll;
        WinButton buttonUpdateAll;
        Panel buttonRowSpacer;
        Control buttonFocusSink;
        TableLayoutPanel tableLayoutPanelActivitiesTitle;
        Label labelActivities;
        Label labelActivitiesInfo;
        DarkHoverPopup activitiesInfoHoverPopup;
        DarkHoverPopup matchOrderHoverPopup;
        DarkHoverPopup matchOrderDelayHoverPopup;
        Label labelActivitiesHint;
        Label labelActivitiesConstraints;

        static readonly ActivityType[] SlotActivityTypeOrder =
        {
            ActivityType.Playing,
            ActivityType.Listening,
            ActivityType.Watching,
            ActivityType.Competing,
        };

        const int MaxEnabledActivities = 5;
        const int ProfileSwitchReconnectDelayMs = 3000;
        const int ActivityToolbarRowHeight = 25;
        const int ToolbarCellWidthPadding = 6;
        const int MatchOrderDelayInputWidth = 52;
        const int MatchOrderLabelTopMargin = 5;
        const int MatchOrderNumericTopMargin = 2;
        const int MatchOrderCheckBoxNudgeDown = 2;
        const int MatchOrderGroupLeftMargin = 8;

        static void FitToolbarRowCell(Control control, ContentAlignment? labelAlign = null, int? fixedWidth = null)
        {
            int width = fixedWidth ?? MeasureToolbarCellWidth(control);
            control.AutoSize = false;
            control.Dock = DockStyle.Fill;
            control.MinimumSize = new Size(width, ActivityToolbarRowHeight);
            control.MaximumSize = new Size(0, ActivityToolbarRowHeight);
            control.Margin = Padding.Empty;
            if (control is Label label && labelAlign.HasValue)
                label.TextAlign = labelAlign.Value;
        }

        static int MeasureToolbarCellWidth(Control control)
        {
            if (control is Label label)
                return TextRenderer.MeasureText(label.Text, label.Font).Width + ToolbarCellWidthPadding;

            return control.PreferredSize.Width + ToolbarCellWidthPadding;
        }

        bool profileSwitchRecoveryRunning;

        string selectedSlotId;
        bool suppressSlotSelectionChange;
        int slotDragSourceIndex = -1;
        int slotInsertionIndex = -1;
        bool slotInsertionAfter;

        void InitializeSlotSystem()
        {
            DetachLegacyEditorDataBindings();

            slotService = new PresenceSlotService(
                defaultID,
                (int)settings.pipe,
                Application.StartupPath + "\\logs",
                GetMultiSlotRpcMode(),
                OnSlotStateChanged,
                BuildPresenceForSlot,
                action =>
                {
                    if (InvokeRequired)
                        BeginInvoke(new MethodInvoker(action));
                    else
                        action();
                },
                OnProfileSwitchRecovery);

            LoadSlotsFromStorage();
            SetupActivitiesPanel();
            buttonConnect.Text = "Connect Slot";
            buttonDisconnect.Text = "Disconnect Slot";
            buttonUpdatePresence.Text = "Update Slot";
            SetupBulkActionButtonsRow();
            WireSlotEditorEvents();
            SetupAlternatingPresetsSection();

            numericUpDownPartySize.TextAlign = HorizontalAlignment.Left;
            numericUpDownPartyMax.TextAlign = HorizontalAlignment.Left;

            if (GetSelectedSlot() == null && slotService.Slots.Count > 0)
                selectedSlotId = slotService.Slots[0].SlotId;

            LoadSelectedSlotToEditor();
            RefreshSlotListView();

            // Cycle Presets left on across restart: snapshot after the editor is hydrated.
            if (settings.alternatingPresetsEnabled)
            {
                activeCyclePresetPath = loadedPresetPath;
                SeedCycleIndexFromCurrentPreset();
                CaptureCycleBaseline();
                UpdateCyclingEditLock();
                SyncAlternatingPresetsTimer();
            }

            SetupMultiSlotRpcModeMenu();
            UpdateGlobalConnectionUi();
            InitializeStatusStrip();
            InitializeMainFormLayout();

            Deactivate -= ClearActionButtonFocus;
            Deactivate += ClearActionButtonFocus;
        }

        void InitializeStatusStrip()
        {
            if (statusStrip == null)
                return;

            statusStrip.Dock = DockStyle.Bottom;
            statusStrip.SizingGrip = false;

            // Spring only expands when AutoSize is false; otherwise the label shrinks to text width
            // and the status label sits beside it on the left instead of the right.
            toolStripStatusLabelUsername.Spring = true;
            toolStripStatusLabelUsername.AutoSize = false;
            toolStripStatusLabelUsername.TextAlign = ContentAlignment.MiddleLeft;
            toolStripStatusLabelStatus.Spring = false;
            toolStripStatusLabelStatus.AutoSize = true;
        }

        string BuildMultiSlotStatusText()
        {
            int connectedCount = slotService.Slots.Count(s => s.IsConnected);
            int enabledCount = slotService.Slots.Count(s => s.Enabled);
            return $"Connected ({connectedCount}/{enabledCount})";
        }

        void LoadSlotsFromStorage()
        {
            slotService.Slots.Clear();

            if (SlotStorage.Exists())
            {
                var config = SlotStorage.Load();
                if (config?.Slots != null)
                {
                    foreach (var slot in config.Slots)
                    {
                        slot.ResetRuntimeState();
                        slotService.Slots.Add(slot);
                    }

                    selectedSlotId = config.SelectedSlotId ?? "";
                    if (!string.IsNullOrWhiteSpace(config.LoadedPresetPath) && File.Exists(config.LoadedPresetPath))
                        loadedPresetPath = config.LoadedPresetPath;
                    else
                        loadedPresetPath = null;

                    if (slotService.Slots.Count > 0)
                        BackfillFromLegacySettings();
                    return;
                }

                if (File.Exists(SlotStorage.ConfigPath))
                    return;
            }

            slotService.Slots.Add(PresenceSlot.FromLegacySettings(settings));
            selectedSlotId = slotService.Slots[0].SlotId;
            SlotStorage.Save(CreateSlotConfig());
        }

        /// <summary>
        /// Migrates only the application ID into slot 1 when slot 1 has no ID yet.
        /// Legacy settings bindings may hold another slot's fields, so never copy the full preset.
        /// </summary>
        void BackfillFromLegacySettings()
        {
            if (slotService.Slots.Count == 0 || string.IsNullOrWhiteSpace(settings.id))
                return;

            var first = slotService.Slots[0];
            if (!string.IsNullOrWhiteSpace(first.ApplicationId))
                return;

            first.ApplicationId = settings.id.Trim();
            SlotStorage.Save(CreateSlotConfig());
        }

        void DetachLegacyEditorDataBindings()
        {
            foreach (Control control in new Control[]
            {
                dateTimePickerTimestampEnd, dateTimePickerTimestampStart, checkBoxTimestampEnd,
                numericUpDownPartySize, numericUpDownPartyMax,
                textBoxSmallURL, textBoxLargeURL, textBoxStateURL, textBoxDetailsURL, textBoxName,
                textBoxButton2Text, textBoxButton2URL, textBoxButton1URL, textBoxButton1Text,
                comboBoxSmallKey, textBoxSmallText, textBoxLargeText, comboBoxLargeKey,
                textBoxState, textBoxDetails,
            })
            {
                control.DataBindings.Clear();
            }
        }

        void SetupActivitiesPanel()
        {
            const int listHeight = 120;
            const int panelHeight = 270;

            labelActivities = new Label
            {
                AutoSize = true,
                Text = "Activities",
            };

            labelActivitiesInfo = new Label
            {
                AutoSize = true,
                Text = "\u24D8",
                Cursor = Cursors.Default,
                Font = new Font(Font.FontFamily, 14f, FontStyle.Regular),
            };
            labelActivitiesInfo.MouseEnter += ActivitiesInfoIcon_MouseEnter;
            labelActivitiesInfo.MouseLeave += ActivitiesInfoIcon_MouseLeave;

            activitiesInfoHoverPopup = new DarkHoverPopup();
            activitiesInfoHoverPopup.MouseEnter += (_, __) => activitiesInfoHoverPopup.CancelHide();
            activitiesInfoHoverPopup.MouseLeave += (_, __) => activitiesInfoHoverPopup.ScheduleHide();

            matchOrderHoverPopup = new DarkHoverPopup();
            matchOrderHoverPopup.MouseEnter += (_, __) => matchOrderHoverPopup.CancelHide();
            matchOrderHoverPopup.MouseLeave += (_, __) => matchOrderHoverPopup.ScheduleHide();

            matchOrderDelayHoverPopup = new DarkHoverPopup();
            matchOrderDelayHoverPopup.MouseEnter += (_, __) => matchOrderDelayHoverPopup.CancelHide();
            matchOrderDelayHoverPopup.MouseLeave += (_, __) => matchOrderDelayHoverPopup.ScheduleHide();

            FitToolbarRowCell(labelActivities, ContentAlignment.MiddleLeft);
            FitToolbarRowCell(labelActivitiesInfo, ContentAlignment.MiddleLeft);
            labelActivitiesInfo.Margin = new Padding(1, 0, 0, 0);

            tableLayoutPanelActivitiesTitle = new TableLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 2,
                RowCount = 1,
                Margin = Padding.Empty,
                Padding = Padding.Empty,
            };
            tableLayoutPanelActivitiesTitle.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            tableLayoutPanelActivitiesTitle.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            tableLayoutPanelActivitiesTitle.RowStyles.Add(new RowStyle(SizeType.Absolute, ActivityToolbarRowHeight));
            tableLayoutPanelActivitiesTitle.Controls.Add(labelActivities, 0, 0);
            tableLayoutPanelActivitiesTitle.Controls.Add(labelActivitiesInfo, 1, 0);

            labelActivitiesHint = new Label
            {
                AutoSize = true,
                Location = new Point(15, 26),
                MaximumSize = new Size(ActivitiesListWidth, 0),
                Text = "",
                Visible = false,
            };

            labelActivitiesConstraints = new Label
            {
                AutoSize = true,
                Location = new Point(15, 44),
                MaximumSize = new Size(ActivitiesListWidth, 0),
                Text = "",
                Visible = false,
            };

            listViewSlots = new ListView
            {
                Location = new Point(15, 72),
                Size = new Size(ActivitiesListWidth, listHeight),
                View = View.Details,
                FullRowSelect = true,
                HideSelection = false,
                MultiSelect = true,
                HeaderStyle = ColumnHeaderStyle.Nonclickable,
                BorderStyle = BorderStyle.FixedSingle,
                AllowDrop = true,
                OwnerDraw = true,
            };
            ControlThemeHelper.EnableListViewDoubleBuffer(listViewSlots);
            listViewSlots.Columns.Add("Name", ActivitiesNameColumnWidth);
            listViewSlots.Columns.Add("Type", ActivitiesTypeColumnWidth);
            listViewSlots.Columns.Add("Application ID", ActivitiesApplicationIdColumnWidth);
            listViewSlots.Columns.Add("Status", ActivitiesStatusColumnWidth);
            listViewSlots.Columns.Add("Enabled", ActivitiesEnabledColumnWidth);
            listViewSlots.SelectedIndexChanged += SlotListSelectionChanged;
            listViewSlots.MouseDown += ListViewSlots_MouseDown;
            listViewSlots.DrawColumnHeader += ListViewSlots_DrawColumnHeader;
            listViewSlots.DrawItem += ListViewSlots_DrawItem;
            listViewSlots.DrawSubItem += ListViewSlots_DrawSubItem;
            listViewSlots.ColumnWidthChanged += (_, __) => listViewSlots.Invalidate(true);
            listViewSlots.GotFocus += ListViewSlots_RepaintSelection;
            listViewSlots.LostFocus += ListViewSlots_RepaintSelection;
            listViewSlots.Enter += ListViewSlots_RepaintSelection;
            listViewSlots.Leave += ListViewSlots_RepaintSelection;
            listViewSlots.ItemDrag += ListViewSlots_ItemDrag;
            listViewSlots.DragEnter += ListViewSlots_DragEnter;
            listViewSlots.DragOver += ListViewSlots_DragOver;
            listViewSlots.DragDrop += ListViewSlots_DragDrop;
            listViewSlots.DragLeave += ListViewSlots_DragLeave;
            listViewSlots.MouseWheel += ListViewSlots_MouseWheelKeepScrollbar;
            listViewSlots.Paint += ListViewSlots_PaintKeepScrollbar;

            flowLayoutPanelSlotActions = new FlowLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                WrapContents = false,
                FlowDirection = FlowDirection.LeftToRight,
                Margin = Padding.Empty,
                Padding = Padding.Empty,
            };

            buttonAddSlot = CreateActionButton("Add Activity", SlotAdd_Click);
            buttonDuplicateSlot = CreateActionButton("Duplicate", SlotDuplicate_Click);
            buttonRemoveSlot = CreateActionButton("Remove", SlotRemove_Click);
            buttonToggleEnabled = CreateActionButton("Disable", SlotToggleEnabled_Click);

            flowLayoutPanelMatchOrder = new FlowLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                WrapContents = false,
                FlowDirection = FlowDirection.LeftToRight,
                Margin = new Padding(MatchOrderGroupLeftMargin, 0, 0, 0),
                Padding = Padding.Empty,
            };

            checkBoxMatchDiscordListOrder = new CheckBox
            {
                AutoSize = true,
                Text = "",
                Margin = Padding.Empty,
            };
            checkBoxMatchDiscordListOrder.CheckedChanged += CheckBoxMatchDiscordListOrder_CheckedChanged;

            panelMatchOrderCheckBoxHost = new Panel
            {
                Margin = Padding.Empty,
            };
            panelMatchOrderCheckBoxHost.Controls.Add(checkBoxMatchDiscordListOrder);
            panelMatchOrderCheckBoxHost.Resize += (_, __) => LayoutMatchOrderCheckBoxHost();
            LayoutMatchOrderCheckBoxHost();

            labelMatchOrderText = new Label
            {
                AutoSize = true,
                Text = "Match Order",
                Margin = new Padding(0, MatchOrderLabelTopMargin, 0, 0),
                Cursor = Cursors.Default,
            };
            labelMatchOrderText.MouseEnter += MatchOrderText_MouseEnter;
            labelMatchOrderText.MouseLeave += MatchOrderText_MouseLeave;
            labelMatchOrderText.Click += MatchOrderText_Click;

            labelMatchOrderDelay = new Label
            {
                AutoSize = true,
                Text = "Delay",
                Margin = new Padding(8, MatchOrderLabelTopMargin, 3, 0),
            };
            labelMatchOrderDelay.MouseEnter += MatchOrderDelay_MouseEnter;
            labelMatchOrderDelay.MouseLeave += MatchOrderDelay_MouseLeave;

            numericUpDownMatchOrderDelay = new NumericUpDown
            {
                Minimum = 1,
                Maximum = 300,
                DecimalPlaces = 0,
                TextAlign = HorizontalAlignment.Left,
                Size = new Size(MatchOrderDelayInputWidth - 2, 22),
                Margin = new Padding(0, MatchOrderNumericTopMargin, 0, 0),
            };
            numericUpDownMatchOrderDelay.ValueChanged += NumericUpDownMatchOrderDelay_ValueChanged;

            flowLayoutPanelMatchOrder.Controls.AddRange(new Control[]
            {
                panelMatchOrderCheckBoxHost,
                labelMatchOrderText,
                labelMatchOrderDelay,
                numericUpDownMatchOrderDelay,
            });

            flowLayoutPanelSlotActions.Controls.AddRange(new Control[]
            {
                buttonAddSlot,
                buttonDuplicateSlot,
                buttonRemoveSlot,
                buttonToggleEnabled,
                flowLayoutPanelMatchOrder,
            });

            LayoutMatchOrderCheckBoxHost();

            SyncMatchListOrderToggle();
            SetupHelpMenuForkRepo();

            panelActivities = new Panel
            {
                Location = new Point(0, 24),
                Size = new Size(ClientSize.Width, panelHeight),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                TabStop = true,
            };
            buttonFocusSink = new Panel
            {
                Size = Size.Empty,
                TabStop = true,
                TabIndex = 9999,
            };
            panelActivities.Controls.Add(buttonFocusSink);
            panelActivities.Controls.Add(tableLayoutPanelActivitiesTitle);
            panelActivities.Controls.Add(labelActivitiesHint);
            panelActivities.Controls.Add(labelActivitiesConstraints);
            panelActivities.Controls.Add(listViewSlots);
            panelActivities.Controls.Add(flowLayoutPanelSlotActions);

            Controls.Add(panelActivities);
            panelActivities.BringToFront();

            ShiftControlsDown(panelHeight);
            ClientSize = new Size(ClientSize.Width, ClientSize.Height + panelHeight);
            MinimumSize = new Size(ClientSize.Width, ClientSize.Height);

            ApplyActivitiesPanelTheme();
        }

        void LayoutMatchOrderCheckBoxHost()
        {
            if (panelMatchOrderCheckBoxHost == null || checkBoxMatchDiscordListOrder == null)
                return;

            panelMatchOrderCheckBoxHost.Size = new Size(
                checkBoxMatchDiscordListOrder.PreferredSize.Width,
                ActivityToolbarRowHeight);

            int labelHeight = labelMatchOrderText != null
                ? labelMatchOrderText.PreferredHeight
                : TextRenderer.MeasureText("Match Order", Font).Height;
            int labelCenterY = MatchOrderLabelTopMargin + labelHeight / 2;
            int checkTop = labelCenterY - checkBoxMatchDiscordListOrder.Height / 2 + MatchOrderCheckBoxNudgeDown;

            checkBoxMatchDiscordListOrder.Location = new Point(
                0,
                Math.Max(0, Math.Min(checkTop, panelMatchOrderCheckBoxHost.Height - checkBoxMatchDiscordListOrder.Height)));
        }

        void ListViewSlots_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left || string.IsNullOrEmpty(selectedSlotId))
                return;

            if (listViewSlots.HitTest(e.Location).Item == null)
                RestoreListSelection(ensureVisible: false);
        }

        void ListViewSlots_RepaintSelection(object sender, EventArgs e)
        {
            listViewSlots?.Invalidate();
        }

        void ListViewSlots_DrawColumnHeader(object sender, DrawListViewColumnHeaderEventArgs e)
        {
            e.DrawDefault = true;
        }

        void ListViewSlots_DrawItem(object sender, DrawListViewItemEventArgs e)
        {
            if (!Properties.Settings.Default.darkMode)
            {
                e.DrawDefault = true;
                return;
            }

            // Details view: cells are painted in DrawSubItem (avoids clip / partial-invalidate wipe bugs).
            e.DrawDefault = false;
        }

        void ListViewSlots_DrawSubItem(object sender, DrawListViewSubItemEventArgs e)
        {
            if (!Properties.Settings.Default.darkMode)
            {
                e.DrawDefault = true;
                return;
            }

            // Paint only this cell; extend the last column to the client right edge so
            // multi-select highlight is full-width without wiping other columns on partial redraw.
            var backgroundBounds = e.Bounds;
            if (e.ColumnIndex == listViewSlots.Columns.Count - 1)
                backgroundBounds.Width = Math.Max(backgroundBounds.Width, listViewSlots.ClientSize.Width - backgroundBounds.X);

            using (var brush = new SolidBrush(GetActivitiesListRowBackColor(e.Item)))
                e.Graphics.FillRectangle(brush, backgroundBounds);

            DrawActivitiesListCellText(e.Graphics, e.SubItem.Text, e.Bounds, e.Item);
            e.DrawDefault = false;
        }

        static readonly TextFormatFlags ActivitiesListCellTextFlags =
            TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis |
            TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix;

        void DrawActivitiesListCellText(Graphics graphics, string text, Rectangle bounds, ListViewItem item)
        {
            var textBounds = bounds;
            textBounds.X += 4;
            textBounds.Width = Math.Max(0, bounds.Width - 4);
            TextRenderer.DrawText(
                graphics,
                text ?? "",
                listViewSlots.Font,
                textBounds,
                GetActivitiesListRowForeColor(item),
                ActivitiesListCellTextFlags);
        }

        Color GetActivitiesListRowBackColor(ListViewItem item)
        {
            if (!item.Selected)
                return CurrentColors.BgTextFields;

            return CurrentColors.BgListSelected;
        }

        Color GetActivitiesListRowForeColor(ListViewItem item) => CurrentColors.TextColor;

        SlotConfig CreateSlotConfig()
        {
            return SlotStorage.FromSlots(slotService.Slots, selectedSlotId, loadedPresetPath);
        }

        void ListViewSlots_ItemDrag(object sender, ItemDragEventArgs e)
        {
            if (IsCyclingEditLocked())
                return;

            if (e.Item is ListViewItem item)
            {
                slotDragSourceIndex = item.Index;
                listViewSlots.DoDragDrop(item, DragDropEffects.Move);
                slotDragSourceIndex = -1;
            }
        }

        void ListViewSlots_DragEnter(object sender, DragEventArgs e)
        {
            if (IsCyclingEditLocked())
            {
                e.Effect = DragDropEffects.None;
                return;
            }

            e.Effect = e.Data.GetDataPresent(typeof(ListViewItem)) ? DragDropEffects.Move : DragDropEffects.None;
        }

        void ListViewSlots_DragOver(object sender, DragEventArgs e)
        {
            if (IsCyclingEditLocked())
            {
                e.Effect = DragDropEffects.None;
                return;
            }
            if (!e.Data.GetDataPresent(typeof(ListViewItem)))
            {
                e.Effect = DragDropEffects.None;
                return;
            }

            var point = listViewSlots.PointToClient(new Point(e.X, e.Y));
            int nearestIndex = listViewSlots.InsertionMark.NearestIndex(point);
            slotInsertionIndex = -1;
            slotInsertionAfter = false;

            if (nearestIndex > -1)
            {
                Rectangle bounds = listViewSlots.GetItemRect(nearestIndex);
                slotInsertionAfter = point.Y > bounds.Top + (bounds.Height / 2);
                slotInsertionIndex = nearestIndex;
                listViewSlots.InsertionMark.Index = nearestIndex;
                listViewSlots.InsertionMark.AppearsAfterItem = slotInsertionAfter;
                listViewSlots.InsertionMark.Color = CurrentColors.TextColor;
            }
            else if (listViewSlots.Items.Count > 0)
            {
                slotInsertionIndex = listViewSlots.Items.Count - 1;
                slotInsertionAfter = true;
                listViewSlots.InsertionMark.Index = slotInsertionIndex;
                listViewSlots.InsertionMark.AppearsAfterItem = true;
                listViewSlots.InsertionMark.Color = CurrentColors.TextColor;
            }

            e.Effect = DragDropEffects.Move;
        }

        void ListViewSlots_DragLeave(object sender, EventArgs e)
        {
            listViewSlots.InsertionMark.Index = -1;
            slotInsertionIndex = -1;
        }

        void ListViewSlots_DragDrop(object sender, DragEventArgs e)
        {
            listViewSlots.InsertionMark.Index = -1;

            if (IsCyclingEditLocked())
                return;

            if (slotDragSourceIndex < 0 || slotDragSourceIndex >= slotService.Slots.Count)
                return;

            int targetIndex = slotInsertionIndex;
            if (targetIndex < 0)
                targetIndex = slotService.Slots.Count - 1;

            if (slotInsertionAfter)
                targetIndex++;

            MoveSlotToIndex(slotDragSourceIndex, targetIndex);
            slotInsertionIndex = -1;
        }

        void MoveSlotToIndex(int sourceIndex, int targetIndex)
        {
            if (sourceIndex < 0 || sourceIndex >= slotService.Slots.Count)
                return;

            targetIndex = Math.Max(0, Math.Min(targetIndex, slotService.Slots.Count));
            if (sourceIndex == targetIndex || sourceIndex + 1 == targetIndex)
                return;

            var slot = slotService.Slots[sourceIndex];
            slotService.Slots.RemoveAt(sourceIndex);
            if (targetIndex > sourceIndex)
                targetIndex--;

            slotService.Slots.Insert(targetIndex, slot);
            selectedSlotId = slot.SlotId;
            RefreshSlotListView();
            SaveSlotsToStorage();
        }

        void SetupBulkActionButtonsRow()
        {
            const int buttonGapPx = 4;

            tableLayoutPanelButtons.ColumnCount = 4;
            tableLayoutPanelButtons.ColumnStyles.Clear();
            tableLayoutPanelButtons.RowCount = 3;
            tableLayoutPanelButtons.RowStyles.Clear();
            tableLayoutPanelButtons.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            tableLayoutPanelButtons.RowStyles.Add(new RowStyle(SizeType.Absolute, buttonGapPx));
            tableLayoutPanelButtons.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            tableLayoutPanelButtons.SetCellPosition(buttonConnect, new TableLayoutPanelCellPosition(0, 0));
            tableLayoutPanelButtons.SetCellPosition(buttonDisconnect, new TableLayoutPanelCellPosition(1, 0));
            tableLayoutPanelButtons.SetCellPosition(buttonUpdatePresence, new TableLayoutPanelCellPosition(3, 0));

            if (buttonRowSpacer == null)
            {
                buttonRowSpacer = new Panel
                {
                    Margin = Padding.Empty,
                    Height = buttonGapPx,
                    Dock = DockStyle.Fill,
                };
                tableLayoutPanelButtons.Controls.Add(buttonRowSpacer, 0, 1);
                tableLayoutPanelButtons.SetColumnSpan(buttonRowSpacer, 4);
            }

            buttonConnectAll = CreateActionButton("Connect All", SlotConnectAll_Click);
            buttonDisconnectAll = CreateActionButton("Disconnect All", SlotDisconnectAll_Click);
            buttonUpdateAll = CreateActionButton("Update All", SlotUpdateAll_Click);

            tableLayoutPanelButtons.Controls.Add(buttonConnectAll, 0, 2);
            tableLayoutPanelButtons.Controls.Add(buttonDisconnectAll, 1, 2);
            tableLayoutPanelButtons.Controls.Add(buttonUpdateAll, 3, 2);

            tableLayoutPanelButtons.Padding = new Padding(15, LayoutBottomGap, 15, LayoutBottomGap);

            SyncPairedActionButtonWidths();
        }

        void SyncPairedActionButtonWidths()
        {
            int connectWidth = MeasureActionButtonWidth("Connect Slot", "Connect All");
            int disconnectWidth = MeasureActionButtonWidth("Disconnect Slot", "Disconnect All");
            int updateWidth = MeasureActionButtonWidth("Update Slot", "Update All");
            const int buttonGapPx = 4;
            const int halfGap = buttonGapPx / 2;

            SetFixedActionButton(buttonConnect, connectWidth, new Padding(0, 4, halfGap, 0));
            SetFixedActionButton(buttonDisconnect, disconnectWidth, new Padding(halfGap, 4, halfGap, 0));
            SetFixedActionButton(buttonUpdatePresence, updateWidth, new Padding(halfGap, 4, 0, 0));
            SetFixedActionButton(buttonConnectAll, connectWidth, new Padding(0, 0, halfGap, 4));
            SetFixedActionButton(buttonDisconnectAll, disconnectWidth, new Padding(halfGap, 0, halfGap, 4));
            SetFixedActionButton(buttonUpdateAll, updateWidth, new Padding(halfGap, 0, 0, 4));

            tableLayoutPanelButtons.ColumnStyles.Clear();
            tableLayoutPanelButtons.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, connectWidth + halfGap));
            tableLayoutPanelButtons.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, disconnectWidth + buttonGapPx));
            tableLayoutPanelButtons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            tableLayoutPanelButtons.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, updateWidth + halfGap));
            tableLayoutPanelButtons.MinimumSize = new Size(ClientSize.Width, 0);
            tableLayoutPanelButtons.PerformLayout();
        }

        int MeasureActionButtonWidth(params string[] labels)
        {
            int max = 0;
            Font font = buttonConnect.Font;
            foreach (string label in labels)
                max = Math.Max(max, TextRenderer.MeasureText(label, font).Width + 16);

            return max;
        }

        static void SetFixedActionButton(WinButton button, int width, Padding margin)
        {
            if (button == null)
                return;

            int height = button.MinimumSize.Height > 0 ? button.MinimumSize.Height : 25;
            button.AutoSize = false;
            button.Dock = DockStyle.Fill;
            button.Margin = margin;
            button.MinimumSize = new Size(width, height);
            button.MaximumSize = new Size(width, height);
            button.Size = new Size(width, height);
        }

        void EnsureFormFitsBottomControls()
        {
            if (layoutInitialized)
                return;

            int bottom = tableLayoutPanelButtons.Bottom;
            if (statusStrip != null)
                bottom = Math.Max(bottom, statusStrip.Top);

            int neededHeight = bottom + LayoutBottomGap;
            if (ClientSize.Height < neededHeight)
            {
                ClientSize = new Size(ClientSize.Width, neededHeight);
                MinimumSize = new Size(MinimumSize.Width, neededHeight);
            }
        }

        void WireSlotEditorEvents()
        {
            textBoxID.TextChanged += SlotEditor_IdChanged;
            textBoxName.TextChanged += SlotEditor_NameChanged;

            EventHandler commitOnLeave = (sender, e) =>
            {
                if (loading)
                    return;
                CommitEditorToSelectedSlot(persistToDisk: true);
            };

            foreach (Control control in new Control[]
            {
                textBoxID, textBoxName, textBoxDetails, textBoxDetailsURL, textBoxState, textBoxStateURL,
                textBoxLargeText, textBoxLargeURL, textBoxSmallText, textBoxSmallURL,
                textBoxButton1Text, textBoxButton1URL, textBoxButton2Text, textBoxButton2URL,
                comboBoxLargeKey, comboBoxSmallKey, comboBoxType, comboBoxDisplay,
                numericUpDownPartySize, numericUpDownPartyMax,
                dateTimePickerTimestampStart, dateTimePickerTimestampEnd,
            })
            {
                control.Leave += commitOnLeave;
            }

            checkBoxTimestampEnd.Leave += commitOnLeave;
            foreach (RadioButton btn in new[] { radioButtonLastConnection, radioButtonStartTime, radioButtonPresence, radioButtonLocalTime, radioButtonCustom })
                btn.CheckedChanged += (sender, e) =>
                {
                    if (loading || !((RadioButton)sender).Checked)
                        return;
                    CommitEditorToSelectedSlot(persistToDisk: true);
                };
        }

        void SlotEditor_IdChanged(object sender, EventArgs e)
        {
            if (loading || IsCyclingEditLocked())
                return;

            EnsureSelectedSlot();
            var slot = GetSelectedSlot();
            if (slot == null)
                return;

            slot.ApplicationId = textBoxID.Text.Trim();
            UpdateListItemForSlot(slot, keepEditorFocus: true);
            UpdateSelectedSlotIdFieldColor(slot);
            UpdateGlobalConnectionUi();
            SaveSlotsToStorage();
        }

        void SlotEditor_NameChanged(object sender, EventArgs e)
        {
            if (loading || IsCyclingEditLocked())
                return;

            var slot = GetSelectedSlot();
            if (slot == null)
                return;

            slot.Name = textBoxName.Text;
            slot.Label = string.IsNullOrWhiteSpace(slot.Name)
                ? $"Activity {slotService.Slots.IndexOf(slot) + 1}"
                : slot.Name;

            UpdateListItemForSlot(slot, keepEditorFocus: true);
            SaveSlotsToStorage();
        }

        void EnsureSelectedSlot()
        {
            if (GetSelectedSlot() != null || slotService.Slots.Count == 0)
                return;

            selectedSlotId = slotService.Slots[0].SlotId;
            LoadSelectedSlotToEditor();
            RestoreListSelection(keepEditorFocus: true);
        }

        void CommitEditorToSelectedSlot(bool persistToDisk = false)
        {
            SaveEditorToSelectedSlot(refreshList: false);
            var slot = GetSelectedSlot();
            if (slot != null)
                UpdateListItemForSlot(slot);

            if (persistToDisk)
                PersistSlotsToDisk();
        }

        void PersistSlotsToDisk()
        {
            // Mid-cycle charts can briefly contain overlapping App IDs; keep the pre-cycle baseline on disk.
            if (IsCyclingEditLocked() && cycleBaselineSlots != null)
            {
                SlotStorage.Save(SlotStorage.FromSlots(
                    cycleBaselineSlots,
                    cycleBaselineSelectedId,
                    loadedPresetPath));
                return;
            }

            SlotStorage.Save(CreateSlotConfig());
        }

        void SaveSlotsToStorage()
        {
            if (loading)
            {
                SlotStorage.Save(CreateSlotConfig());
                return;
            }

            if (!IsCyclingEditLocked())
                SaveEditorToSelectedSlot(refreshList: false);

            PersistSlotsToDisk();
        }

        void ApplyActivitiesPanelTheme()
        {
            if (panelActivities == null)
                return;

            panelActivities.BackColor = CurrentColors.BgColor;
            labelActivities.ForeColor = CurrentColors.TextColor;
            if (labelActivitiesInfo != null)
                labelActivitiesInfo.ForeColor = CurrentColors.TextInactive;

            UpdateCyclingStatusBanner();

            if (listViewSlots != null)
            {
                listViewSlots.BackColor = CurrentColors.BgTextFields;
                listViewSlots.ForeColor = CurrentColors.TextColor;
                WinApi.ApplyListViewThemeScrollbars(listViewSlots);
                listViewSlots.Invalidate(true);
            }

            if (checkBoxMatchDiscordListOrder != null)
                checkBoxMatchDiscordListOrder.ForeColor = CurrentColors.TextColor;

            if (labelMatchOrderText != null)
            {
                labelMatchOrderText.ForeColor = IsCyclingEditLocked() || !IsMultiSlotRpcMode()
                    ? CurrentColors.TextInactive
                    : CurrentColors.TextColor;
            }

            if (labelMatchOrderDelay != null)
            {
                bool delayActive = IsMultiSlotRpcMode() &&
                    checkBoxMatchDiscordListOrder != null &&
                    checkBoxMatchDiscordListOrder.Checked &&
                    !IsCyclingEditLocked();
                labelMatchOrderDelay.ForeColor = delayActive
                    ? CurrentColors.TextColor
                    : CurrentColors.TextInactive;
            }

            if (numericUpDownMatchOrderDelay != null)
            {
                numericUpDownMatchOrderDelay.BackColor = CurrentColors.BgTextFields;
                numericUpDownMatchOrderDelay.ForeColor = CurrentColors.TextColor;
            }

            UpdateMatchOrderDelayControls();

            foreach (var button in new[]
            {
                buttonAddSlot, buttonDuplicateSlot, buttonRemoveSlot, buttonToggleEnabled,
                buttonConnect, buttonDisconnect, buttonUpdatePresence,
                buttonConnectAll, buttonDisconnectAll, buttonUpdateAll,
            })
            {
                ThemeActionButton(button);
            }
        }

        void ThemeActionButton(WinButton button)
        {
            if (button == null)
                return;

            if (settings.darkMode)
            {
                button.FlatStyle = FlatStyle.Flat;
                button.ForeColor = CurrentColors.TextColor;
                button.BackColor = CurrentColors.BgButton;
                button.UseVisualStyleBackColor = false;
                button.FlatAppearance.BorderSize = 0;
                button.FlatAppearance.MouseOverBackColor = CurrentColors.BgButtonMouseOver;
                button.FlatAppearance.MouseDownBackColor = CurrentColors.BgButtonMouseDown;
            }
            else
            {
                button.FlatStyle = FlatStyle.Standard;
                button.BackColor = SystemColors.Control;
                button.ForeColor = SystemColors.ControlText;
                button.UseVisualStyleBackColor = true;
            }

            button.Invalidate();
        }

        WinButton CreateActionButton(string text, EventHandler handler)
        {
            var button = new WinButton
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Text = text,
                Margin = new Padding(0, 0, 4, 0),
                MinimumSize = new Size(0, 25),
            };
            button.Click += handler;
            button.Paint += DisabedTextPaint;
            ThemeActionButton(button);
            return button;
        }

        string GetActivityTypeDisplayName(ActivityType type)
        {
            foreach (var presenceType in presenceTypes)
            {
                if (presenceType.Type == type)
                    return presenceType.Name;
            }

            return type.ToString();
        }

        ActivityType? GetNextAvailableActivityType()
        {
            var used = new HashSet<ActivityType>(slotService.Slots.Select(s => s.ActivityType));
            foreach (var type in SlotActivityTypeOrder)
            {
                if (!used.Contains(type))
                    return type;
            }

            return null;
        }

        bool TryValidateEnabledSlotsForConnect(out string errorMessage)
        {
            errorMessage = null;
            var enabledSlots = slotService.Slots.Where(s => s.Enabled).ToList();

            var duplicateIds = enabledSlots
                .Select(s => NormalizeApplicationId(s.ApplicationId))
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .GroupBy(id => id)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToList();

            if (IsMultiSlotRpcMode() && duplicateIds.Count > 0)
            {
                errorMessage = ActivitiesUiText.DuplicateApplicationIdConnectAll;
                return false;
            }

            int playingCount = enabledSlots.Count(s => s.ActivityType == ActivityType.Playing);
            if (playingCount > 1)
            {
                errorMessage = ActivitiesUiText.MultiplePlayingConnectAll;
                return false;
            }

            return true;
        }

        bool TryValidateSlotForConnect(PresenceSlot slot, out string errorMessage)
        {
            errorMessage = null;
            if (slot == null)
            {
                errorMessage = "No activity selected.";
                return false;
            }

            if (!slot.Enabled && IsMultiSlotRpcMode())
            {
                errorMessage = "This activity is disabled. Enable it before connecting.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(slot.ApplicationId))
            {
                errorMessage = "Enter an Application ID before connecting.";
                return false;
            }

            string appId = NormalizeApplicationId(slot.ApplicationId);
            if (IsMultiSlotRpcMode())
            {
                bool duplicateId = slotService.Slots.Any(s =>
                    s.SlotId != slot.SlotId &&
                    s.Enabled &&
                    NormalizeApplicationId(s.ApplicationId) == appId);
                if (duplicateId)
                {
                    errorMessage = ActivitiesUiText.DuplicateApplicationIdConnectSlot;
                    return false;
                }
            }

            if (slot.ActivityType == ActivityType.Playing)
            {
                bool otherPlaying = slotService.Slots.Any(s =>
                    s.SlotId != slot.SlotId &&
                    s.ActivityType == ActivityType.Playing &&
                    (IsMultiSlotRpcMode() ? s.Enabled : true));
                if (otherPlaying)
                {
                    errorMessage = ActivitiesUiText.MultiplePlayingConnectSlot;
                    return false;
                }
            }

            return true;
        }

        static string NormalizeApplicationId(string applicationId) =>
            string.IsNullOrWhiteSpace(applicationId) ? "" : applicationId.Trim();

        bool SlotHasDuplicateApplicationId(PresenceSlot slot)
        {
            if (!IsMultiSlotRpcMode() || slot == null)
                return false;

            string appId = NormalizeApplicationId(slot.ApplicationId);
            if (string.IsNullOrEmpty(appId))
                return false;

            return slotService.Slots.Any(s =>
                s.SlotId != slot.SlotId &&
                s.Enabled &&
                NormalizeApplicationId(s.ApplicationId) == appId);
        }

        void ShowSlotConstraintMessage(string message) =>
            QuietMessageBox.Show(this, message, Strings.information, MessageBoxButtons.OK, MessageBoxIcon.Information);

        void ShiftControlsDown(int offset)
        {
            foreach (Control control in Controls)
            {
                if (control == panelActivities || control == menuStrip || control == statusStrip)
                    continue;

                control.Top += offset;
            }

            tableLayoutPanelButtons.Top += offset;
        }

        PresenceSlot GetSelectedSlot() =>
            string.IsNullOrEmpty(selectedSlotId) ? null : slotService.GetSlot(selectedSlotId);

        List<PresenceSlot> GetSelectedSlotsInChartOrder()
        {
            var result = new List<PresenceSlot>();
            if (listViewSlots == null)
                return result;

            foreach (ListViewItem item in listViewSlots.SelectedItems.Cast<ListViewItem>().OrderBy(i => i.Index))
            {
                var slot = slotService.GetSlot((string)item.Tag);
                if (slot != null)
                    result.Add(slot);
            }

            return result;
        }

        bool TryAssignEnabledForNewSlot(PresenceSlot slot, bool wantEnabled)
        {
            if (slot == null)
                return false;

            if (!wantEnabled)
            {
                slot.Enabled = false;
                return true;
            }

            if (slotService.Slots.Count(s => s.Enabled) >= MaxEnabledActivities)
            {
                slot.Enabled = false;
                return false;
            }

            slot.Enabled = true;
            return true;
        }

        void RefreshSlotListView()
        {
            if (listViewSlots == null)
                return;

            int topIndex = listViewSlots.TopItem?.Index ?? 0;

            suppressSlotSelectionChange = true;
            listViewSlots.BeginUpdate();
            listViewSlots.Items.Clear();

            foreach (var slot in slotService.Slots)
            {
                var item = new ListViewItem(slot.Label)
                {
                    Tag = slot.SlotId,
                };
                item.SubItems.Add(GetActivityTypeDisplayName(slot.ActivityType));
                item.SubItems.Add(slot.ApplicationId ?? "");
                item.SubItems.Add(GetSlotStatusText(slot));
                item.SubItems.Add(GetSlotEnabledText(slot));
                listViewSlots.Items.Add(item);
            }

            listViewSlots.EndUpdate();
            RestoreListSelection(keepEditorFocus: true, ensureVisible: false);
            RestoreListViewScroll(topIndex);
            UpdateSlotActionButtons();
            listViewSlots.Invalidate(true);
            SyncActivitiesListScrollbar();
        }

        void UpdateListItemForSlot(PresenceSlot slot, bool keepEditorFocus = false)
        {
            if (listViewSlots == null || slot == null)
                return;

            listViewSlots.BeginUpdate();
            try
            {
                foreach (ListViewItem item in listViewSlots.Items)
                {
                    if ((string)item.Tag != slot.SlotId)
                        continue;

                    item.Text = slot.Label;
                    while (item.SubItems.Count < 5)
                        item.SubItems.Add("");

                    item.SubItems[1].Text = GetActivityTypeDisplayName(slot.ActivityType);
                    item.SubItems[2].Text = slot.ApplicationId ?? "";
                    item.SubItems[3].Text = GetSlotStatusText(slot);
                    item.SubItems[4].Text = GetSlotEnabledText(slot);
                    break;
                }
            }
            finally
            {
                listViewSlots.EndUpdate();
                listViewSlots.Invalidate(true);
                SyncActivitiesListScrollbar();
            }
        }

        void RestoreListViewScroll(int topIndex)
        {
            if (listViewSlots == null || listViewSlots.Items.Count == 0)
                return;

            int restoreIndex = Math.Max(0, Math.Min(topIndex, listViewSlots.Items.Count - 1));
            listViewSlots.TopItem = listViewSlots.Items[restoreIndex];
        }

        void RestoreListSelection(bool keepEditorFocus = false, bool ensureVisible = false)
        {
            if (listViewSlots == null)
                return;

            suppressSlotSelectionChange = true;

            if (!string.IsNullOrEmpty(selectedSlotId))
            {
                foreach (ListViewItem item in listViewSlots.Items)
                {
                    item.Selected = (string)item.Tag == selectedSlotId;
                    if (item.Selected)
                    {
                        item.Focused = true;
                        if (ensureVisible)
                            item.EnsureVisible();
                    }
                }
            }

            suppressSlotSelectionChange = false;

            if (!keepEditorFocus)
                listViewSlots.Focus();
        }

        static string GetSlotEnabledText(PresenceSlot slot) =>
            slot != null && slot.Enabled ? "true" : "false";

        string GetSlotStatusText(PresenceSlot slot)
        {
            if (slotService?.IsMatchOrderDelaying(slot) == true &&
                slot.ConnectionState != SlotConnectionState.Connecting &&
                slot.ConnectionState != SlotConnectionState.UpdatingPresence)
                return "Delaying";

            if (!string.IsNullOrEmpty(slot.LastError) && slot.ConnectionState == SlotConnectionState.Error)
                return "Error";

            switch (slot.ConnectionState)
            {
                case SlotConnectionState.Connected: return "Connected";
                case SlotConnectionState.Connecting: return "Connecting";
                case SlotConnectionState.UpdatingPresence: return "Updating";
                case SlotConnectionState.Error: return "Error";
                default: return "Disconnected";
            }
        }

        void OnSlotStateChanged(PresenceSlot slot)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new MethodInvoker(() => OnSlotStateChanged(slot)));
                return;
            }

            UpdateListItemForSlot(slot);
            UpdateGlobalConnectionUi();
            SyncAlternatingPresetsTimer();
            if (IsCyclingEditLocked())
                UpdateCyclingStatusBanner();

            if (slot.SlotId == selectedSlotId)
                UpdateSelectedSlotIdFieldColor(slot);
        }

        void UpdateGlobalConnectionUi()
        {
            var slot = GetSelectedSlot();
            bool selectedConnected = slot?.IsConnected == true;
            bool selectedBusy = slot != null && (slot.ConnectionState == SlotConnectionState.Connecting ||
                slot.ConnectionState == SlotConnectionState.UpdatingPresence);

            buttonConnect.Enabled = slot != null && (IsMultiSlotRpcMode() ? slot.Enabled : true) &&
                !selectedConnected && !selectedBusy &&
                !string.IsNullOrWhiteSpace(slot.ApplicationId);
            buttonDisconnect.Enabled = slot != null && (selectedConnected || selectedBusy);
            buttonUpdatePresence.Enabled = selectedConnected;

            bool anyConnectable = slotService.Slots.Any(s => s.Enabled && !string.IsNullOrWhiteSpace(s.ApplicationId) &&
                !s.IsConnected &&
                s.ConnectionState != SlotConnectionState.Connecting &&
                s.ConnectionState != SlotConnectionState.UpdatingPresence);
            bool anyConnected = slotService.Slots.Any(s => s.IsConnected);
            bool anyConnecting = slotService.Slots.Any(s => s.ConnectionState == SlotConnectionState.Connecting ||
                s.ConnectionState == SlotConnectionState.UpdatingPresence);
            bool anyDelaying = slotService.Slots.Any(s => slotService.IsMatchOrderDelaying(s));

            bool multiSlotMode = IsMultiSlotRpcMode();

            if (buttonConnectAll != null)
                buttonConnectAll.Enabled = multiSlotMode && anyConnectable && !cycleAutoconnectInProgress;
            if (buttonDisconnectAll != null)
                buttonDisconnectAll.Enabled = multiSlotMode && (anyConnected || anyConnecting || anyDelaying);
            if (buttonUpdateAll != null)
                buttonUpdateAll.Enabled = multiSlotMode && anyConnected;

            textBoxID.ReadOnly = selectedConnected || selectedBusy;

            SyncTrayMenuForRpcMode();
            bool anyActivePresence = anyConnected || anyConnecting || anyDelaying;
            if (IsMultiSlotRpcMode())
            {
                trayMenuDisconnect.Enabled = anyActivePresence;
                trayMenuReconnect.Enabled = !cycleAutoconnectInProgress &&
                    (anyConnectable || anyActivePresence);
            }
            else
            {
                // Legacy tray: disconnect whatever is live, not only the selected row.
                trayMenuDisconnect.Enabled = anyActivePresence;
                trayMenuReconnect.Enabled = slotService.Slots.Any(s => !string.IsNullOrWhiteSpace(s.ApplicationId));
            }

            if (slot != null)
                UpdateSelectedSlotIdFieldColor(slot);
            else
                textBoxID.BackColor = CurrentColors.BgTextFields;

            bool anyError = slotService.Slots.Any(s => s.ConnectionState == SlotConnectionState.Error);

            if (anyConnected)
            {
                ConnectionManager.State = ConnectionState.Connected;
                toolStripStatusLabelStatus.Text = IsMultiSlotRpcMode()
                    ? BuildMultiSlotStatusText()
                    : Strings.statusConnected;
                var connected = slotService.Slots.FirstOrDefault(s => s.IsConnected && s.Client != null);
                if (connected?.Client != null)
                {
                    toolStripStatusLabelUsername.Text = connected.Client.CurrentUser?.Username ?? "";
                    trayIcon.Text = $"{res.GetString("trayIcon.Text")}{(Program.IsSecondInstance ? " (2)" : "")}\n{connected.Client.CurrentUser?.Username ?? ""}";
                }
            }
            else if (anyConnecting)
            {
                ConnectionManager.State = ConnectionState.Connecting;
                toolStripStatusLabelStatus.Text = Strings.statusConnecting;
            }
            else             if (anyError)
            {
                ConnectionManager.State = ConnectionState.Error;
                toolStripStatusLabelStatus.Text = Strings.statusError;
            }
            else
            {
                ConnectionManager.State = ConnectionState.Disconnected;
                toolStripStatusLabelStatus.Text = Strings.statusDisconnected;
                toolStripStatusLabelUsername.Text = "";
                trayIcon.Text = $"{res.GetString("trayIcon.Text")}{(Program.IsSecondInstance ? " (2)" : "")}";
            }

            if (IsCyclingEditLocked())
            {
                buttonConnect.Enabled = false;
                buttonDisconnect.Enabled = false;
                buttonUpdatePresence.Enabled = false;
                // While Cycle Presets is on, Connect All remains available to start the timer,
                // and Disconnect All remains available to cancel the session.
                if (buttonUpdateAll != null)
                    buttonUpdateAll.Enabled = false;
            }
        }

        void UpdateSelectedSlotIdFieldColor(PresenceSlot slot)
        {
            if (slot == null || textBoxID == null)
                return;

            // While Cycle Presets is on, keep the ID field neutral (no green/red/duplicate flash).
            if (IsCyclingEditLocked())
            {
                textBoxID.BackColor = CurrentColors.BgTextFields;
                return;
            }

            if (IsMultiSlotRpcMode() && SlotHasDuplicateApplicationId(slot))
            {
                textBoxID.BackColor = CurrentColors.BgTextFieldsDuplicateId;
                return;
            }

            switch (slot.ConnectionState)
            {
                case SlotConnectionState.Connected:
                    textBoxID.BackColor = CurrentColors.BgTextFieldsSuccess;
                    break;
                case SlotConnectionState.Error:
                    textBoxID.BackColor = CurrentColors.BgTextFieldsError;
                    break;
                default:
                    textBoxID.BackColor = CurrentColors.BgTextFields;
                    break;
            }
        }

        void SyncActivitiesListScrollbar()
        {
            if (listViewSlots == null)
                return;

            // Keep the chart scrollbar present during cycling in Multi-RP only (not Legacy).
            WinApi.SetListViewVerticalScrollbarForced(
                listViewSlots,
                IsCyclingEditLocked() && IsMultiSlotRpcMode());

            // Legacy Status column fills remaining client width (shrinks when scrollbar appears).
            if (!IsMultiSlotRpcMode())
                SyncActivitiesListColumns();
        }

        void ListViewSlots_MouseWheelKeepScrollbar(object sender, MouseEventArgs e)
        {
            if (!IsCyclingEditLocked() || !IsMultiSlotRpcMode())
                return;

            BeginInvoke(new MethodInvoker(SyncActivitiesListScrollbar));
        }

        void ListViewSlots_PaintKeepScrollbar(object sender, PaintEventArgs e)
        {
            if (!IsCyclingEditLocked() || !IsMultiSlotRpcMode() ||
                listViewSlots == null || !listViewSlots.IsHandleCreated)
                return;

            WinApi.SetListViewVerticalScrollbarForced(listViewSlots, true);
        }

        void UpdateSlotActionButtons()
        {
            if (buttonAddSlot == null || buttonDuplicateSlot == null || buttonRemoveSlot == null)
                return;

            bool locked = IsCyclingEditLocked();
            bool hasSelection = GetSelectedSlotsInChartOrder().Count > 0 || GetSelectedSlot() != null;
            bool multiSlotMode = IsMultiSlotRpcMode();
            buttonAddSlot.Enabled = !locked;
            buttonDuplicateSlot.Enabled = !locked && hasSelection;
            buttonRemoveSlot.Enabled = !locked && hasSelection;
            if (buttonToggleEnabled != null)
                buttonToggleEnabled.Visible = multiSlotMode;
            UpdateToggleEnabledButtonText();
            if (locked && buttonToggleEnabled != null)
                buttonToggleEnabled.Enabled = false;
        }

        void UpdateToggleEnabledButtonText()
        {
            if (buttonToggleEnabled == null)
                return;

            var selected = GetSelectedSlotsInChartOrder();
            buttonToggleEnabled.Enabled = selected.Count > 0;
            buttonToggleEnabled.Text = selected.Any(s => !s.Enabled) ? "Enable" : "Disable";
        }

        bool ShouldMatchDiscordListOrder() =>
            IsMultiSlotRpcMode() && settings.matchDiscordListOrder;

        int GetMatchListOrderDelayMs() =>
            (int)(Math.Max(1, settings.matchListOrderDelaySeconds) * 1000);

        void SyncActivitiesInfoIcon()
        {
            if (labelActivitiesInfo == null)
                return;

            bool showInfo = IsMultiSlotRpcMode();
            labelActivitiesInfo.Visible = showInfo;

            if (!showInfo)
                activitiesInfoHoverPopup?.HideNow();
        }

        void ActivitiesInfoIcon_MouseEnter(object sender, EventArgs e)
        {
            if (!IsMultiSlotRpcMode())
                return;

            activitiesInfoHoverPopup.CancelHide();
            activitiesInfoHoverPopup.ShowLines(
                ActivitiesInfoContent.GetInfoLines(),
                labelActivitiesInfo.PointToScreen(Point.Empty));
        }

        void ActivitiesInfoIcon_MouseLeave(object sender, EventArgs e) =>
            activitiesInfoHoverPopup?.ScheduleHide();

        void MatchOrderText_MouseEnter(object sender, EventArgs e)
        {
            if (IsCyclingEditLocked() || !IsMultiSlotRpcMode() || labelMatchOrderText == null)
                return;

            matchOrderHoverPopup.CancelHide();
            matchOrderHoverPopup.ShowLines(
                ActivitiesInfoContent.GetMatchOrderLines(),
                labelMatchOrderText.PointToScreen(new Point(0, labelMatchOrderText.Height)),
                RectangleToScreen(ClientRectangle),
                new Padding(10, 4, 10, 8));
        }

        void MatchOrderText_MouseLeave(object sender, EventArgs e) =>
            matchOrderHoverPopup?.ScheduleHide();

        void MatchOrderDelay_MouseEnter(object sender, EventArgs e)
        {
            if (IsCyclingEditLocked() || !IsMultiSlotRpcMode() || labelMatchOrderDelay == null)
                return;

            matchOrderDelayHoverPopup.CancelHide();
            matchOrderDelayHoverPopup.ShowLines(
                ActivitiesInfoContent.GetMatchOrderDelayLines(),
                labelMatchOrderDelay.PointToScreen(new Point(0, labelMatchOrderDelay.Height)),
                RectangleToScreen(ClientRectangle),
                new Padding(10, 4, 10, 8));
        }

        void MatchOrderDelay_MouseLeave(object sender, EventArgs e) =>
            matchOrderDelayHoverPopup?.ScheduleHide();

        void MatchOrderText_Click(object sender, EventArgs e)
        {
            if (IsCyclingEditLocked() || checkBoxMatchDiscordListOrder == null || !checkBoxMatchDiscordListOrder.Enabled)
                return;

            checkBoxMatchDiscordListOrder.Checked = !checkBoxMatchDiscordListOrder.Checked;
        }

        void SetupHelpMenuForkRepo()
        {
            if (gitHubPageToolStripMenuItem == null)
                return;

            gitHubPageToolStripMenuItem.Text = "CustomRP GitHub Repo";

            int index = helpToolStripMenuItem.DropDownItems.IndexOf(gitHubPageToolStripMenuItem);
            if (index < 0)
                return;

            foreach (ToolStripItem item in helpToolStripMenuItem.DropDownItems)
            {
                if (item is ToolStripMenuItem menuItem &&
                    menuItem.Tag as string == "https://github.com/landnthrn/Discord-Multi-CustomRP")
                    return;
            }

            var forkRepoItem = new ToolStripMenuItem("Multi-RP's GitHub Fork Repo")
            {
                Tag = "https://github.com/landnthrn/Discord-Multi-CustomRP",
            };
            forkRepoItem.Click += OpenSite;
            helpToolStripMenuItem.DropDownItems.Insert(index + 1, forkRepoItem);
        }

        void SyncMatchListOrderToggle()
        {
            if (checkBoxMatchDiscordListOrder == null)
                return;

            bool available = IsMultiSlotRpcMode();
            if (flowLayoutPanelMatchOrder != null)
                flowLayoutPanelMatchOrder.Visible = available;

            if (!available)
            {
                // Hide Match Order in Legacy without clearing the saved Multi-RP preference.
                syncingMatchListOrderToggle = true;
                try
                {
                    checkBoxMatchDiscordListOrder.Checked = false;
                }
                finally
                {
                    syncingMatchListOrderToggle = false;
                }

                checkBoxMatchDiscordListOrder.Enabled = false;
                UpdateMatchOrderDelayControls();
                return;
            }

            checkBoxMatchDiscordListOrder.Enabled = !IsCyclingEditLocked();

            syncingMatchListOrderToggle = true;
            try
            {
                checkBoxMatchDiscordListOrder.Checked = settings.matchDiscordListOrder;
            }
            finally
            {
                syncingMatchListOrderToggle = false;
            }

            syncingMatchListOrderDelay = true;
            try
            {
                decimal delay = Math.Max(1, Math.Min(300, settings.matchListOrderDelaySeconds));
                numericUpDownMatchOrderDelay.Value = delay;
            }
            finally
            {
                syncingMatchListOrderDelay = false;
            }

            UpdateMatchOrderDelayControls();
        }

        void UpdateMatchOrderDelayControls()
        {
            if (labelMatchOrderDelay == null || numericUpDownMatchOrderDelay == null)
                return;

            bool locked = IsCyclingEditLocked();
            bool enabled = !locked && IsMultiSlotRpcMode() && checkBoxMatchDiscordListOrder.Checked;
            numericUpDownMatchOrderDelay.Enabled = enabled;

            // Always leave the Delay label enabled so ForeColor sticks; tooltips/clicks are gated separately.
            labelMatchOrderDelay.Enabled = true;
            Color delayColor = enabled ? CurrentColors.TextColor : CurrentColors.TextInactive;
            labelMatchOrderDelay.ForeColor = delayColor;
            numericUpDownMatchOrderDelay.ForeColor = delayColor;
            numericUpDownMatchOrderDelay.Invalidate();
        }

        void CheckBoxMatchDiscordListOrder_CheckedChanged(object sender, EventArgs e)
        {
            if (loading || syncingMatchListOrderToggle)
                return;

            settings.matchDiscordListOrder = checkBoxMatchDiscordListOrder.Checked;
            Utils.SaveSettings();
            UpdateMatchOrderDelayControls();

            // Turning Match Order off mid-sequence: drop the delay queue and proceed normally.
            if (!checkBoxMatchDiscordListOrder.Checked && slotService != null)
            {
                slotService.CancelMatchOrderDelayAndProceed();
                RefreshSlotListView();
                UpdateGlobalConnectionUi();
            }
        }

        void NumericUpDownMatchOrderDelay_ValueChanged(object sender, EventArgs e)
        {
            if (loading || syncingMatchListOrderDelay)
                return;

            settings.matchListOrderDelaySeconds = numericUpDownMatchOrderDelay.Value;
            Utils.SaveSettings();
        }

        void SlotToggleEnabled_Click(object sender, EventArgs e)
        {
            if (IsCyclingEditLocked())
                return;

            var targets = GetSelectedSlotsInChartOrder();
            if (targets.Count == 0)
                return;

            bool enabling = targets.Any(s => !s.Enabled);
            if (enabling)
            {
                int enabledCount = slotService.Slots.Count(s => s.Enabled);
                bool hitLimit = false;
                foreach (var slot in targets.Where(s => !s.Enabled))
                {
                    if (enabledCount >= MaxEnabledActivities)
                    {
                        hitLimit = true;
                        break;
                    }

                    slot.Enabled = true;
                    enabledCount++;
                    UpdateListItemForSlot(slot);
                }

                if (hitLimit)
                    ShowSlotConstraintMessage(ActivitiesUiText.MaxEnabledActivities);
            }
            else
            {
                foreach (var slot in targets)
                {
                    slot.Enabled = false;
                    UpdateListItemForSlot(slot);
                }
            }

            UpdateToggleEnabledButtonText();
            UpdateGlobalConnectionUi();
            SaveSlotsToStorage();
        }

        void SlotListSelectionChanged(object sender, EventArgs e)
        {
            if (suppressSlotSelectionChange)
                return;

            if (listViewSlots.SelectedItems.Count == 0)
            {
                UpdateToggleEnabledButtonText();
                UpdateSlotActionButtons();
                listViewSlots.Invalidate(true);
                return;
            }

            string newSlotId;
            if (listViewSlots.FocusedItem != null && listViewSlots.FocusedItem.Selected)
                newSlotId = (string)listViewSlots.FocusedItem.Tag;
            else if (!string.IsNullOrEmpty(selectedSlotId) &&
                     listViewSlots.SelectedItems.Cast<ListViewItem>().Any(i => (string)i.Tag == selectedSlotId))
                newSlotId = selectedSlotId;
            else
                newSlotId = (string)listViewSlots.SelectedItems[listViewSlots.SelectedItems.Count - 1].Tag;

            if (newSlotId != selectedSlotId)
            {
                SaveEditorToSelectedSlot(refreshList: false);
                selectedSlotId = newSlotId;
                LoadSelectedSlotToEditor();
                UpdateListItemForSlot(GetSelectedSlot());
                UpdateGlobalConnectionUi();
                SaveSlotsToStorage();
            }

            UpdateToggleEnabledButtonText();
            UpdateSlotActionButtons();
            listViewSlots?.Invalidate(true);
        }

        void LoadSelectedSlotToEditor()
        {
            var slot = GetSelectedSlot();
            if (slot == null)
            {
                ClearEditorForNoSlot();
                UpdateToggleEnabledButtonText();
                UpdateGlobalConnectionUi();
                UpdateSlotActionButtons();
                return;
            }

            suppressSlotSelectionChange = true;
            loading = true;

            textBoxID.Text = slot.ApplicationId;
            textBoxName.Text = slot.Name;
            textBoxDetails.Text = slot.Details;
            textBoxDetailsURL.Text = slot.DetailsUrl;
            textBoxState.Text = slot.State;
            textBoxStateURL.Text = slot.StateUrl;
            numericUpDownPartySize.Value = Math.Max(0, slot.PartySize);
            numericUpDownPartyMax.Value = Math.Max(0, slot.PartyMax);
            comboBoxLargeKey.Text = slot.LargeImageKey;
            textBoxLargeText.Text = slot.LargeImageText;
            textBoxLargeURL.Text = slot.LargeImageUrl;
            comboBoxSmallKey.Text = slot.SmallImageKey;
            textBoxSmallText.Text = slot.SmallImageText;
            textBoxSmallURL.Text = slot.SmallImageUrl;
            textBoxButton1Text.Text = slot.Button1Text;
            textBoxButton1URL.Text = slot.Button1Url;
            textBoxButton2Text.Text = slot.Button2Text;
            textBoxButton2URL.Text = slot.Button2Url;
            comboBoxType.SelectedValue = slot.ActivityType;
            comboBoxDisplay.SelectedValue = slot.StatusDisplay;

            switch (slot.TimestampType)
            {
                case TimestampType.SinceLastConnection: radioButtonLastConnection.Checked = true; break;
                case TimestampType.SincePresenceUpdate: radioButtonPresence.Checked = true; break;
                case TimestampType.SinceStartup: radioButtonStartTime.Checked = true; break;
                case TimestampType.LocalTime: radioButtonLocalTime.Checked = true; break;
                case TimestampType.Custom: radioButtonCustom.Checked = true; break;
            }

            if (slot.CustomTimestamp.CompareTo(new DateTime(1969, 1, 1)) != 0)
                dateTimePickerTimestampStart.Value = slot.CustomTimestamp;
            checkBoxTimestampEnd.Checked = slot.CustomTimestampEndEnabled;
            if (slot.CustomTimestampEnd.CompareTo(new DateTime(1969, 1, 1)) != 0)
                dateTimePickerTimestampEnd.Value = slot.CustomTimestampEnd;

            tableLayoutPanelCustomTimestamps.Enabled = slot.TimestampType == TimestampType.Custom;
            dateTimePickerTimestampEnd.Enabled = checkBoxTimestampEnd.Checked;
            UpdateToggleEnabledButtonText();
            UpdateSelectedSlotIdFieldColor(slot);

            loading = false;
            suppressSlotSelectionChange = false;
            UpdateGlobalConnectionUi();
        }

        void ClearEditorForNoSlot()
        {
            suppressSlotSelectionChange = true;
            loading = true;

            textBoxID.Text = "";
            textBoxName.Text = "";
            textBoxDetails.Text = "";
            textBoxDetailsURL.Text = "";
            textBoxState.Text = "";
            textBoxStateURL.Text = "";
            numericUpDownPartySize.Value = 0;
            numericUpDownPartyMax.Value = 0;
            comboBoxLargeKey.Text = "";
            textBoxLargeText.Text = "";
            textBoxLargeURL.Text = "";
            comboBoxSmallKey.Text = "";
            textBoxSmallText.Text = "";
            textBoxSmallURL.Text = "";
            textBoxButton1Text.Text = "";
            textBoxButton1URL.Text = "";
            textBoxButton2Text.Text = "";
            textBoxButton2URL.Text = "";
            textBoxID.BackColor = CurrentColors.BgTextFields;

            loading = false;
            suppressSlotSelectionChange = false;
        }

        void SaveEditorToSelectedSlot(bool refreshList = true)
        {
            // While Cycle Presets is on, the editor is locked — never write it back into slots.
            if (IsCyclingEditLocked())
                return;

            FlushEditorToSelectedSlot(refreshList);
        }

        void FlushEditorToSelectedSlot(bool refreshList = true)
        {
            var slot = GetSelectedSlot();
            if (slot == null || loading)
                return;

            slot.ApplicationId = textBoxID.Text.Trim();
            slot.Name = textBoxName.Text;
            slot.Details = textBoxDetails.Text;
            slot.DetailsUrl = textBoxDetailsURL.Text;
            slot.State = textBoxState.Text;
            slot.StateUrl = textBoxStateURL.Text;
            slot.PartySize = (int)numericUpDownPartySize.Value;
            slot.PartyMax = (int)numericUpDownPartyMax.Value;
            slot.LargeImageKey = comboBoxLargeKey.Text;
            slot.LargeImageText = textBoxLargeText.Text;
            slot.LargeImageUrl = textBoxLargeURL.Text;
            slot.SmallImageKey = comboBoxSmallKey.Text;
            slot.SmallImageText = textBoxSmallText.Text;
            slot.SmallImageUrl = textBoxSmallURL.Text;
            slot.Button1Text = textBoxButton1Text.Text;
            slot.Button1Url = textBoxButton1URL.Text;
            slot.Button2Text = textBoxButton2Text.Text;
            slot.Button2Url = textBoxButton2URL.Text;

            if (comboBoxType.SelectedValue is ActivityType activityType)
                slot.Type = (int)activityType;
            if (comboBoxDisplay.SelectedValue is StatusDisplayType displayType)
                slot.Display = (int)displayType;

            foreach (RadioButton btn in new[] { radioButtonLastConnection, radioButtonStartTime, radioButtonPresence, radioButtonLocalTime, radioButtonCustom })
            {
                if (btn.Checked)
                    slot.Timestamps = (int)(TimestampType)btn.Tag;
            }

            slot.CustomTimestamp = dateTimePickerTimestampStart.Value;
            slot.CustomTimestampEndEnabled = checkBoxTimestampEnd.Checked;
            slot.CustomTimestampEnd = dateTimePickerTimestampEnd.Value;

            if (string.IsNullOrWhiteSpace(slot.Label))
                slot.Label = string.IsNullOrWhiteSpace(slot.Name) ? $"Activity {slotService.Slots.IndexOf(slot) + 1}" : slot.Name;

            if (refreshList)
                UpdateListItemForSlot(slot);
        }

        PresenceBuilder.BuildResult BuildPresenceForSlot(PresenceSlot slot)
        {
            return PresenceBuilder.Build(
                slot,
                slot.TimestampConnected,
                slot.TimestampStarted,
                slot.CustomTimestamp,
                slot.CustomTimestampEnd,
                slot.CustomTimestampEndEnabled,
                textBoxDetailsURL.MaxLength,
                textBoxStateURL.MaxLength,
                textBoxLargeURL.MaxLength,
                textBoxSmallURL.MaxLength,
                textBoxButton1URL.MaxLength,
                textBoxButton2URL.MaxLength);
        }

        void SlotAdd_Click(object sender, EventArgs e)
        {
            if (IsCyclingEditLocked())
                return;

            SaveEditorToSelectedSlot(refreshList: false);
            var nextType = GetNextAvailableActivityType() ?? ActivityType.Playing;

            int index = slotService.Slots.Count + 1;
            var slot = PresenceSlot.CreateDefault($"Activity {index}", "", (int)settings.pipe);
            slot.Type = (int)nextType;
            slot.Label = $"Activity {index}";
            slot.Name = "";
            slot.ApplicationId = "";
            TryAssignEnabledForNewSlot(slot, wantEnabled: true);
            slotService.Slots.Add(slot);
            selectedSlotId = slot.SlotId;
            LoadSelectedSlotToEditor();
            RefreshSlotListView();
            SaveSlotsToStorage();
        }

        void SlotDuplicate_Click(object sender, EventArgs e)
        {
            if (IsCyclingEditLocked())
                return;

            var sources = GetSelectedSlotsInChartOrder();
            if (sources.Count == 0)
                return;

            SaveEditorToSelectedSlot();
            PresenceSlot lastCopy = null;
            foreach (var source in sources)
            {
                var copy = source.Clone($"{source.Label} copy");
                TryAssignEnabledForNewSlot(copy, source.Enabled);
                slotService.Slots.Add(copy);
                lastCopy = copy;
            }

            selectedSlotId = lastCopy?.SlotId;
            LoadSelectedSlotToEditor();
            RefreshSlotListView();
            SaveSlotsToStorage();
        }

        void SlotRemove_Click(object sender, EventArgs e)
        {
            if (IsCyclingEditLocked())
                return;

            var targets = GetSelectedSlotsInChartOrder();
            if (targets.Count == 0)
                return;

            string confirm = targets.Count == 1
                ? $"Remove activity \"{targets[0].Label}\"?"
                : $"Remove {targets.Count} selected activities?";
            if (QuietMessageBox.Show(this, confirm, Application.ProductName, MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question) != DialogResult.Yes)
                return;

            int firstIndex = targets
                .Select(t => slotService.Slots.IndexOf(t))
                .Where(i => i >= 0)
                .DefaultIfEmpty(0)
                .Min();

            foreach (var slot in targets)
            {
                slotService.DisconnectSlot(slot);
                slotService.Slots.Remove(slot);
            }

            if (slotService.Slots.Count == 0)
                selectedSlotId = null;
            else
            {
                int nextIndex = Math.Min(firstIndex, slotService.Slots.Count - 1);
                selectedSlotId = slotService.Slots[nextIndex].SlotId;
            }

            LoadSelectedSlotToEditor();
            RefreshSlotListView();
            UpdateSlotActionButtons();
            SaveSlotsToStorage();
        }

        void AutoconnectOnStartup()
        {
            if (IsMultiSlotRpcMode())
            {
                if (TryValidateEnabledSlotsForConnect(out _))
                    _ = slotService.ConnectAllEnabledSlotsAsync(ShouldMatchDiscordListOrder(), GetMatchListOrderDelayMs);
                return;
            }

            var slot = GetSelectedSlot();
            if (slot == null || !slot.Enabled || string.IsNullOrWhiteSpace(slot.ApplicationId))
                return;

            if (!TryValidateSlotForConnect(slot, out _))
                return;

            slotService.ConnectSlot(slot);
        }

        void ApplyNewPreset()
        {
            if (IsMultiSlotRpcMode())
            {
                DisconnectAllSlots();

                for (int i = 0; i < slotService.Slots.Count; i++)
                    slotService.Slots[i].ClearToNewPresetDefaults(i + 1);

                LoadSelectedSlotToEditor();
                RefreshSlotListView();
                SaveSlotsToStorage();
                UpdateGlobalConnectionUi();
                return;
            }

            var slot = GetSelectedSlot();
            if (slot?.IsConnected == true)
                DisconnectSlot(slot);

            if (slot != null)
            {
                slot.ClearToNewPresetDefaults(slotService.Slots.IndexOf(slot) + 1);
                LoadSelectedSlotToEditor();
                RefreshSlotListView();
                SaveSlotsToStorage();
                UpdateGlobalConnectionUi();
            }
        }

        void OnProfileSwitchRecovery()
        {
            _ = RunProfileSwitchRecoveryAsync();
        }

        async Task RunProfileSwitchRecoveryAsync()
        {
            if (!IsMultiSlotRpcMode() || profileSwitchRecoveryRunning)
                return;

            profileSwitchRecoveryRunning = true;
            slotService.SetProfileRecoveryActive(true);
            try
            {
                PerformDisconnectAllLikeButton();
                await Task.Delay(ProfileSwitchReconnectDelayMs);
                await PerformConnectAllLikeButtonAsync();
            }
            finally
            {
                slotService.SetProfileRecoveryActive(false);
                profileSwitchRecoveryRunning = false;
            }
        }

        void PerformDisconnectAllLikeButton()
        {
            slotService.DisconnectAllSlots();
            localTimeTimer.Stop();
            UpdateGlobalConnectionUi();
        }

        void SyncTrayMenuForRpcMode()
        {
            if (trayMenuDisconnect == null || trayMenuReconnect == null)
                return;

            if (IsMultiSlotRpcMode())
            {
                trayMenuDisconnect.Text = "Disconnect All";
                trayMenuReconnect.Text = "Reconnect All";
            }
            else
            {
                trayMenuDisconnect.Text = "Disconnect";
                trayMenuReconnect.Text = "Reconnect";
            }
        }

        PresenceSlot GetLegacyTrayConnectionTarget()
        {
            if (slotService == null)
                return null;

            if (!string.IsNullOrEmpty(legacyTrayLastConnectedSlotId))
            {
                var remembered = slotService.GetSlot(legacyTrayLastConnectedSlotId);
                if (remembered != null && !string.IsNullOrWhiteSpace(remembered.ApplicationId))
                    return remembered;
            }

            return slotService.Slots.FirstOrDefault(s =>
                    s.IsConnected ||
                    s.ConnectionState == SlotConnectionState.Connecting ||
                    s.ConnectionState == SlotConnectionState.UpdatingPresence) ??
                slotService.Slots.FirstOrDefault(s => !string.IsNullOrWhiteSpace(s.ApplicationId));
        }

        void NoteTrayCancelledCyclePresets()
        {
            if (IsCyclingEditLocked())
                trayReconnectShouldRestoreCycle = true;
        }

        void TryRestoreCyclePresetsAfterTrayReconnect()
        {
            if (!trayReconnectShouldRestoreCycle)
                return;

            trayReconnectShouldRestoreCycle = false;

            if (checkBoxAlternatingEnabled == null || checkBoxAlternatingEnabled.Checked)
                return;

            if (string.IsNullOrWhiteSpace(settings?.alternatingPresetsFolder) ||
                !Directory.Exists(settings.alternatingPresetsFolder))
                return;

            checkBoxAlternatingEnabled.Checked = true;
        }

        void TrayMenuDisconnect_Click(object sender, EventArgs e)
        {
            if (IsMultiSlotRpcMode())
            {
                NoteTrayCancelledCyclePresets();
                SlotDisconnectAll_Click(sender, e);
                return;
            }

            // Remember the live slot before cycle cancel / disconnect so Reconnect can restore it.
            var liveTarget = slotService?.Slots.FirstOrDefault(s =>
                s.IsConnected ||
                s.ConnectionState == SlotConnectionState.Connecting ||
                s.ConnectionState == SlotConnectionState.UpdatingPresence);

            NoteTrayCancelledCyclePresets();
            if (IsCyclingEditLocked())
                CancelCyclePresetsSession(restoreBaseline: true);

            if (liveTarget != null)
                legacyTrayLastConnectedSlotId = liveTarget.SlotId;

            var target = !string.IsNullOrEmpty(legacyTrayLastConnectedSlotId)
                ? slotService?.GetSlot(legacyTrayLastConnectedSlotId)
                : null;
            if (target != null)
                DisconnectSlot(target);

            localTimeTimer.Stop();
            UpdateGlobalConnectionUi();
            Analytics.TrackEvent("Disconnected (tray legacy)");
        }

        async void TrayMenuReconnect_Click(object sender, EventArgs e)
        {
            if (IsMultiSlotRpcMode())
            {
                if (cycleAutoconnectInProgress)
                    return;

                NoteTrayCancelledCyclePresets();
                if (IsCyclingEditLocked())
                    CancelCyclePresetsSession(restoreBaseline: true);

                PerformDisconnectAllLikeButton();
                if (!TryValidateEnabledSlotsForConnect(out _))
                {
                    UpdateGlobalConnectionUi();
                    TryRestoreCyclePresetsAfterTrayReconnect();
                    return;
                }

                await PerformConnectAllLikeButtonAsync();
                TryRestoreCyclePresetsAfterTrayReconnect();
                Analytics.TrackEvent("Reconnected all slots (tray)");
                return;
            }

            NoteTrayCancelledCyclePresets();
            if (IsCyclingEditLocked())
                CancelCyclePresetsSession(restoreBaseline: true);

            var target = GetLegacyTrayConnectionTarget();
            if (target == null)
            {
                TryRestoreCyclePresetsAfterTrayReconnect();
                return;
            }

            selectedSlotId = target.SlotId;
            LoadSelectedSlotToEditor();
            SaveEditorToSelectedSlot();
            SaveSlotsToStorage();

            if (!TryValidateSlotForConnect(target, out _))
            {
                UpdateGlobalConnectionUi();
                TryRestoreCyclePresetsAfterTrayReconnect();
                return;
            }

            slotService.ReconnectSlot(target);
            UpdateGlobalConnectionUi();
            TryRestoreCyclePresetsAfterTrayReconnect();
            Analytics.TrackEvent("Reconnected (tray legacy)");
        }

        async Task PerformConnectAllLikeButtonAsync()
        {
            SaveEditorToSelectedSlot();
            SaveSlotsToStorage();

            if (!TryValidateEnabledSlotsForConnect(out string error))
            {
                ShowSlotConstraintMessage(error);
                return;
            }

            await slotService.ConnectAllEnabledSlotsAsync(ShouldMatchDiscordListOrder(), GetMatchListOrderDelayMs);
        }

        async void SlotConnectAll_Click(object sender, EventArgs e)
        {
            if (!IsMultiSlotRpcMode() || cycleAutoconnectInProgress)
                return;

            await PerformConnectAllLikeButtonAsync();
            Analytics.TrackEvent("Connected all slots");
        }

        void SlotDisconnectAll_Click(object sender, EventArgs e)
        {
            if (!IsMultiSlotRpcMode())
                return;

            if (IsCyclingEditLocked())
            {
                // Cancel the cycle session (restores pre-cycle chart), then disconnect everything.
                CancelCyclePresetsSession(restoreBaseline: true);
                PerformDisconnectAllLikeButton();
                Analytics.TrackEvent("Disconnected all slots (cancelled cycle)");
                return;
            }

            PerformDisconnectAllLikeButton();
            Analytics.TrackEvent("Disconnected all slots");
        }

        async void SlotUpdateAll_Click(object sender, EventArgs e)
        {
            if (IsCyclingEditLocked() || !IsMultiSlotRpcMode())
                return;

            SaveEditorToSelectedSlot();
            SaveSlotsToStorage();
            await slotService.UpdateAllEnabledSlotsAsync(
                matchListOrder: ShouldMatchDiscordListOrder(),
                getListeningWatchingDelayMs: GetMatchListOrderDelayMs);
        }

        bool InitSlot(PresenceSlot slot) => slotService.InitSlot(slot);

        bool SetPresenceForSlot(PresenceSlot slot)
        {
            SaveEditorToSelectedSlot();
            bool result = slotService.SetPresenceForSlot(slot);

            if (slotService.Slots.Any(s => s.TimestampType == TimestampType.LocalTime && s.IsConnected))
            {
                localTimeTimer.Interval = DateTime.Today.AddDays(1).AddSeconds(5).Subtract(DateTime.Now).TotalMilliseconds;
                localTimeTimer.Start();
            }

            return result;
        }

        void DisconnectSlot(PresenceSlot slot) => slotService.DisconnectSlot(slot);

        void DisconnectAllSlots()
        {
            slotService.DisconnectAllSlots();
            localTimeTimer.Stop();
        }

        void UpdateAllEnabledSlots() => slotService.UpdateAllEnabledSlots();

        void ImportPresetAsSlot(Preset preset, bool addAsNewSlot = true)
        {
            SaveEditorToSelectedSlot();

            if (addAsNewSlot)
            {
                int nextIndex = slotService.Slots.Count + 1;
                string label = string.IsNullOrWhiteSpace(preset.Name)
                    ? $"Activity {nextIndex}"
                    : preset.Name;

                var slot = PresenceSlot.FromPreset(preset, (int)settings.pipe, label);
                bool wantEnabled = !preset.EnabledSpecified || preset.Enabled;
                TryAssignEnabledForNewSlot(slot, wantEnabled);

                slotService.Slots.Add(slot);
                selectedSlotId = slot.SlotId;
            }
            else
            {
                var slot = GetSelectedSlot();
                if (slot == null)
                {
                    slot = PresenceSlot.FromPreset(preset, (int)settings.pipe);
                    slotService.Slots.Add(slot);
                    selectedSlotId = slot.SlotId;
                }
                else
                {
                    ApplyPresetToSlot(slot, preset);
                }
            }

            LoadSelectedSlotToEditor();
            RefreshSlotListView();
            RestoreListSelection(keepEditorFocus: true, ensureVisible: true);
            UpdateSlotActionButtons();
            SaveSlotsToStorage();
        }

        void ReplaceChartWithCrpPreset(Preset preset)
        {
            SaveEditorToSelectedSlot(refreshList: false);
            DisconnectAllSlots();
            slotService.Slots.Clear();

            var slot = PresenceSlot.FromPreset(preset, (int)settings.pipe);
            bool wantEnabled = !preset.EnabledSpecified || preset.Enabled;
            TryAssignEnabledForNewSlot(slot, wantEnabled);
            if (string.IsNullOrWhiteSpace(slot.Label))
                slot.Label = string.IsNullOrWhiteSpace(slot.Name) ? "Activity 1" : slot.Name;

            slotService.Slots.Add(slot);
            selectedSlotId = slot.SlotId;
            LoadSelectedSlotToEditor();
            RefreshSlotListView();
            RestoreListSelection(keepEditorFocus: true, ensureVisible: true);
            UpdateSlotActionButtons();
            UpdateGlobalConnectionUi();
            SaveSlotsToStorage();
        }

        static void ApplyPresetToSlot(PresenceSlot slot, Preset preset)
        {
            slot.ApplicationId = preset.ID ?? "";
            slot.Type = preset.Type;
            slot.Display = preset.Display;
            slot.Name = preset.Name ?? "";
            slot.Label = string.IsNullOrWhiteSpace(preset.Name) ? slot.Label : preset.Name;
            slot.Details = preset.Details ?? "";
            slot.DetailsUrl = preset.DetailsURL ?? "";
            slot.State = preset.State ?? "";
            slot.StateUrl = preset.StateURL ?? "";
            slot.PartySize = preset.PartySize;
            slot.PartyMax = preset.PartyMax;
            slot.Timestamps = preset.Timestamps;
            slot.CustomTimestamp = preset.CustomTimestamp;
            slot.CustomTimestampEndEnabled = preset.CustomTimestampEndEnabled;
            slot.CustomTimestampEnd = preset.CustomTimestampEnd;
            slot.LargeImageKey = preset.LargeKey ?? "";
            slot.LargeImageText = preset.LargeText ?? "";
            slot.LargeImageUrl = preset.LargeURL ?? "";
            slot.SmallImageKey = preset.SmallKey ?? "";
            slot.SmallImageText = preset.SmallText ?? "";
            slot.SmallImageUrl = preset.SmallURL ?? "";
            slot.Button1Text = preset.Button1Text ?? "";
            slot.Button1Url = preset.Button1URL ?? "";
            slot.Button2Text = preset.Button2Text ?? "";
            slot.Button2Url = preset.Button2URL ?? "";
        }

        void LoadMultiSlotPreset(MultiSlotPreset preset)
        {
            if (preset == null)
                return;

            loading = true;
            try
            {
                SaveEditorToSelectedSlot(refreshList: false);
                DisconnectAllSlots();
                slotService.Slots.Clear();

                int index = 0;
                foreach (var stored in preset.Slots ?? Array.Empty<StoredPresenceSlot>())
                {
                    var slot = stored.ToPresenceSlot();
                    if (string.IsNullOrWhiteSpace(slot.Label))
                        slot.Label = string.IsNullOrWhiteSpace(slot.Name) ? $"Activity {++index}" : slot.Name;
                    slotService.Slots.Add(slot);
                }

                if (slotService.Slots.Count == 0)
                    slotService.Slots.Add(PresenceSlot.CreateDefault("Activity 1", "", (int)settings.pipe));

                selectedSlotId = string.IsNullOrEmpty(preset.SelectedSlotId)
                    ? slotService.Slots[0].SlotId
                    : slotService.GetSlot(preset.SelectedSlotId)?.SlotId ?? slotService.Slots[0].SlotId;

                if (preset.MatchDiscordListOrder.HasValue)
                    settings.matchDiscordListOrder = preset.MatchDiscordListOrder.Value;

                if (preset.MatchListOrderDelaySeconds.HasValue)
                {
                    decimal delay = Math.Max(1, Math.Min(300, preset.MatchListOrderDelaySeconds.Value));
                    settings.matchListOrderDelaySeconds = delay;
                }

                SyncMatchListOrderToggle();
                Utils.SaveSettings();

                LoadSelectedSlotToEditor();
                RefreshSlotListView();
                UpdateSlotActionButtons();
                UpdateGlobalConnectionUi();
                SaveSlotsToStorage();
            }
            finally
            {
                loading = false;
            }
        }

        void SaveMultiSlotPreset(string filePath, IList<PresenceSlot> slotsToSave = null)
        {
            SaveEditorToSelectedSlot();

            var slots = slotsToSave != null && slotsToSave.Count > 0
                ? slotsToSave
                : (IList<PresenceSlot>)slotService.Slots;

            string selectedId = selectedSlotId;
            if (slotsToSave != null && slotsToSave.Count > 0 &&
                (string.IsNullOrEmpty(selectedId) || !slots.Any(s => s.SlotId == selectedId)))
                selectedId = slots[0].SlotId;

            var preset = new MultiSlotPreset
            {
                SelectedSlotId = selectedId,
                MatchDiscordListOrder = settings.matchDiscordListOrder,
                MatchListOrderDelaySeconds = Math.Max(1, Math.Min(300, settings.matchListOrderDelaySeconds)),
                Slots = slots.Select(SlotStorage.ToStored).ToArray(),
            };

            File.WriteAllText(filePath, new System.Web.Script.Serialization.JavaScriptSerializer { MaxJsonLength = int.MaxValue }.Serialize(preset));
        }
    }
}
