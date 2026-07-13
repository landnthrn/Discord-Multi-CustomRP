namespace CustomRPC
{
    /// <summary>
    /// User-facing Activities / RP mode strings.
    /// </summary>
    static class ActivitiesUiText
    {
        public const string ModeLegacyMenu = "(Legacy)";
        public const string ModeFakePidMenu = "(Multi-RP's)";

        public const string ModeLegacyTooltip = "Uses legacy Custom RP, connect only one activity";
        public const string ModeFakePidTooltip =
            "Supports multiple RP's using unique fake PID per slot to trick Discord into \r\n" +
            "thinking activities are coming from multiple running processes.";

        public const string MaxEnabledActivities =
            "Discord only shows up to 5 enabled activities, disable another activity before enabling this one.";

        public const string DuplicateApplicationIdConnectAll =
            "Multiple enabled activities are using the same Application ID. \r\n\r\n" +
            "Add another ID for whatever one(s) are shared, or use disable.";

        public const string DuplicateApplicationIdConnectSlot =
            "This Application ID is already used by another activity.\r\n\r\n" +
            "Add another ID for whatever one(s) are shared, or use disable.";

        public const string MultiplePlayingConnectAll =
            "Only one enabled Playing activity can appear on Discord at a time.\r\n\r\n" +
            "Disable or change the activity type on extra Playing slot(s) before connecting.";

        public const string MultiplePlayingConnectSlot =
            "Only one enabled Playing activity can appear on Discord at a time.\r\n\r\n" +
            "Disable or change the activity type on extra Playing slot(s) before connecting.";

        public const string LoadCrpChooseMode =
            "Do you want to replace chart with this preset?\r\n" +
            "Otherwise, select 'No' to add this preset as an additional slot in the current chart.";

        public const string LoadCmrpReplaceConfirm =
            "Loading this Multi-RP preset will replace your current activities chart.\r\n\r\n" +
            "Continue?";

        public const string SaveChangesNoLoadedPreset =
            "No preset file is currently loaded.\r\n\r\n" +
            "Use Load Preset first, or use Save Preset to create a new file.";

        public const string SaveChangesConfirm =
            "Save changes to \"{0}\"?";

        public const string CyclePresetsTitle = "Cycle Presets";
        public const string CycleModeLabel = "Cycle Mode";
        public const string CycleModeSlotSwap = "Slot Swap";
        public const string CycleModePresetSwap = "Preset Swap";
        public const string CyclingPresetsActiveBanner = "Cycle Presets is Active";
        public const string CyclingPresetsPendingBanner =
            "Cycle Presets Pending For All Activities Connected";
        public const string CyclingPresetsPendingBannerLegacy =
            "Cycle Presets Pending For Activity Connection";
        public const string CyclingPresetsActiveBannerTooltip =
            "Cycle Presets is running. Controls are locked to ensure any edits don't get replaced.";
        public const string CyclingPresetsPendingBannerTooltip =
            "Waiting for all enabled activities to be connected...";
        public const string CyclingPresetsPendingBannerTooltipLegacy =
            "Waiting for an activity to be connected...";
    }
}
