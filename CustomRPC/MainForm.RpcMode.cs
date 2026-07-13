using System;
using System.Windows.Forms;

namespace CustomRPC
{
    public partial class MainForm
    {
        ToolStripMenuItem multiSlotRpcModeToolStripMenuItem;
        ToolStripMenuItem rpcModeLegacyToolStripMenuItem;
        ToolStripMenuItem rpcModeFakePidToolStripMenuItem;

        void SetupMultiSlotRpcModeMenu()
        {
            menuStrip.ShowItemToolTips = true;

            multiSlotRpcModeToolStripMenuItem = new ToolStripMenuItem("RP Modes");

            rpcModeLegacyToolStripMenuItem = CreateRpcModeMenuItem(
                ActivitiesUiText.ModeLegacyMenu,
                ActivitiesUiText.ModeLegacyTooltip,
                MultiSlotRpcMode.SingleProcess);

            rpcModeFakePidToolStripMenuItem = CreateRpcModeMenuItem(
                ActivitiesUiText.ModeFakePidMenu,
                ActivitiesUiText.ModeFakePidTooltip,
                MultiSlotRpcMode.CustomProcessId);

            multiSlotRpcModeToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[]
            {
                rpcModeLegacyToolStripMenuItem,
                rpcModeFakePidToolStripMenuItem,
            });

            int settingsIndex = settingsToolStripMenuItem.DropDownItems.IndexOf(darkModeToolStripMenuItem);
            if (settingsIndex >= 0)
                settingsToolStripMenuItem.DropDownItems.Insert(settingsIndex, multiSlotRpcModeToolStripMenuItem);
            else
                settingsToolStripMenuItem.DropDownItems.Add(multiSlotRpcModeToolStripMenuItem);

            settingsToolStripMenuItem.DropDownItems.Insert(
                settingsToolStripMenuItem.DropDownItems.IndexOf(multiSlotRpcModeToolStripMenuItem) + 1,
                new ToolStripSeparator());

            UpdateMultiSlotRpcModeMenuChecks();
            UpdateActivitiesPanelForMode();
        }

        ToolStripMenuItem CreateRpcModeMenuItem(string text, string tooltip, MultiSlotRpcMode mode)
        {
            var item = new ToolStripMenuItem(text)
            {
                Tag = mode,
                ToolTipText = tooltip,
            };
            item.Click += RpcModeMenuItem_Click;
            return item;
        }

        void RpcModeMenuItem_Click(object sender, EventArgs e)
        {
            if (!(sender is ToolStripMenuItem item) || !(item.Tag is MultiSlotRpcMode mode))
                return;

            if (GetMultiSlotRpcMode() == mode)
                return;

            bool switchingToMulti = mode != MultiSlotRpcMode.SingleProcess;

            SaveEditorToSelectedSlot(refreshList: false);
            SaveSlotsToStorage();

            slotService?.DisconnectAllSlots();
            settings.multiSlotRpcMode = (int)mode;
            Utils.SaveSettings();

            if (slotService != null)
                slotService.RpcMode = mode;

            // Legacy ignores Enabled; Multi-RP enforces the 5-enabled cap (same as Add/Duplicate).
            if (switchingToMulti)
                EnforceEnabledSlotCapForMultiMode();

            UpdateMultiSlotRpcModeMenuChecks();
            UpdateActivitiesPanelForMode();
            UpdateCyclingEditLock();
            SyncTrayMenuForRpcMode();
            RefreshSlotListView();
            UpdateGlobalConnectionUi();
            SaveSlotsToStorage();
        }

        /// <summary>
        /// Keeps chart order: first MaxEnabledActivities already-Enabled slots stay on; the rest turn off.
        /// </summary>
        void EnforceEnabledSlotCapForMultiMode()
        {
            if (slotService == null)
                return;

            int enabledCount = 0;
            foreach (var slot in slotService.Slots)
            {
                if (!slot.Enabled)
                    continue;

                enabledCount++;
                if (enabledCount > MaxEnabledActivities)
                    slot.Enabled = false;
            }
        }

        MultiSlotRpcMode GetMultiSlotRpcMode()
        {
            int raw = settings.multiSlotRpcMode;
            // 1 was the removed helper-process option — treat as Multi-RP.
            if (raw == (int)MultiSlotRpcMode.CustomProcessId || raw == 1)
                return MultiSlotRpcMode.CustomProcessId;
            if (raw == (int)MultiSlotRpcMode.SingleProcess)
                return MultiSlotRpcMode.SingleProcess;

            return MultiSlotRpcMode.CustomProcessId;
        }

        bool IsMultiSlotRpcMode() => GetMultiSlotRpcMode() != MultiSlotRpcMode.SingleProcess;

        void UpdateMultiSlotRpcModeMenuChecks()
        {
            if (multiSlotRpcModeToolStripMenuItem == null)
                return;

            var mode = GetMultiSlotRpcMode();
            rpcModeLegacyToolStripMenuItem.Checked = mode == MultiSlotRpcMode.SingleProcess;
            rpcModeFakePidToolStripMenuItem.Checked = mode == MultiSlotRpcMode.CustomProcessId;
        }

        void UpdateActivitiesPanelForMode()
        {
            SyncActivitiesInfoIcon();
            SyncMatchListOrderToggle();
            SyncActivitiesListColumns();
            UpdateSlotActionButtons();
            SyncTrayMenuForRpcMode();

            if (layoutInitialized)
                ApplyMainFormLayout();
        }
    }
}
