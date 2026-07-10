namespace CustomRPC
{
    /// <summary>
    /// User-facing Activities / RP mode strings (see docs/project/activities-ui-text.md).
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
    }
}
