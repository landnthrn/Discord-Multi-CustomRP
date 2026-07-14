namespace CustomRPC
{
    static class ActivitiesInfoContent
    {
        const int Indent = 3;
        const int MatchIndent = 1;

        public static TooltipLine[] GetInfoLines() => new[]
        {
            TooltipLine.Blank(),
            TooltipLine.Text("CustomRP by maximmax42", Indent, bold: true),
            TooltipLine.Text("Multi-RP's by landn.thrn", Indent, bold: true),
            TooltipLine.Text("If you found this useful, ", Indent),
            TooltipLine.Text("please see Help tab for repo links and leave a star! :)", Indent),
            TooltipLine.Blank(),
            TooltipLine.WithRuns(Indent, new TooltipRun("Required: ", bold: true)),
            TooltipLine.Text("Each enabled activity needs a unique Application ID", Indent),
            TooltipLine.Blank(),
            TooltipLine.Text("Discord RP Behaviour: ", Indent, bold: true),
            TooltipLine.Text("- Discord displays max 5 activities at once", Indent),
            TooltipLine.Text("- Only 1 playing activity at a time (may override an actual game you're playing)", Indent),
            TooltipLine.Text("- Any amount between 1 - 5 of Competing, Watching, or Listening activities display properly", Indent),
            TooltipLine.Blank(),
            TooltipLine.WithRuns(
                Indent,
                new TooltipRun("Activity Display Order:", bold: true),
                new TooltipRun(" (top to bottom)", italic: true)),
            TooltipLine.Text("1. Competing", Indent),
            TooltipLine.Text("2. Playing", Indent),
            TooltipLine.Text("3. Listening & Watching just depend on connection timing between them, latest connection displays above previous. ", Indent),
            TooltipLine.Blank(),
            TooltipLine.Text("Genuine RP's Info:", Indent, bold: true),
            TooltipLine.Text("RP's from genuine sources like a game, music...etc... may get overridden if you're using all 5 custom RP's. ", Indent),
            TooltipLine.Text("Usually most recent connections take priority over previous ones, ", Indent),
            TooltipLine.Text("but that doesn't seem to always apply for games. ", Indent),
            TooltipLine.Text("Music players & watching are much better at taking priority over custom RP's.", Indent),
            TooltipLine.Blank(),
        };

        public static TooltipLine[] GetMatchOrderLines() => new[]
        {
            TooltipLine.Text("Listening & Watching activities allow a custom order depending on ", MatchIndent),
            TooltipLine.Text("their connection time, (latest connection displays above previous) ", MatchIndent),
            TooltipLine.Blank(),
            TooltipLine.Text("Drag & drop to arrange order of slots. ", MatchIndent),
            TooltipLine.Blank(),
            TooltipLine.WithRuns(MatchIndent, new TooltipRun("If using with Cycle RP's", bold: true)),
            TooltipLine.Text("Make sure to read all Cycle RP's tooltips", MatchIndent),
            TooltipLine.Blank(),
        };

        public static TooltipLine[] GetMatchOrderDelayLines() => new[]
        {
            TooltipLine.Text("Delay is to ensure they're individually connected at the right times. Delay is measured in seconds.", MatchIndent),
            TooltipLine.Text("(Recommended 12(+) second delay to be safe on keeping order) ", MatchIndent),
            TooltipLine.Blank(),
        };

        public static TooltipLine[] GetAlternatingPresetsLines() => GetCyclePresetsLines();

        public static TooltipLine[] GetCyclePresetsLines() => new[]
        {
            TooltipLine.Blank(),
            TooltipLine.Text("Cycle RP's", Indent, bold: true),
            TooltipLine.Text("Cycles connecting different activities from preset files in a folder in order of filename (A–Z), then repeats. If incoming activities match a present activity's application ID, it targets it for it's replacement/outgoing, unless Match Order is enabled.", Indent),
            TooltipLine.Blank(),
            TooltipLine.Text("- Point to a folder that holds presets you want it to cycle through", Indent),
            TooltipLine.Text("- Pick .cmrp for multi-activity presets or .crp for single-activity presets", Indent),
            TooltipLine.Text("- Set the switch interval in minutes and/or seconds", Indent),
            TooltipLine.Text("- Choose a Cycle Mode", Indent),
            TooltipLine.Text("- Timer starts when all enabled activities are connected", Indent),
            TooltipLine.Blank(),
            TooltipLine.Text("Disclaimer", Indent, bold: true),
            TooltipLine.Text("A very low switch interval (e.g. 0min 10sec) can lag Discord itself, especially for Preset Swap mode. Images/GIFs size & resolution, can also impact the lag.", Indent),
            TooltipLine.Blank(),
        };

        public static TooltipLine[] GetCycleModeLines() => new[]
        {
            TooltipLine.Blank(),
            TooltipLine.Text("Cycle Mode", Indent, bold: true),
            TooltipLine.Blank(),
            TooltipLine.Text("Slot Swap", Indent, bold: true),
            TooltipLine.Text("Each switch interval connects one incoming activity, while disconnecting an outgoing.", Indent),
            TooltipLine.Blank(),
            TooltipLine.Text("Continues across intervals until the activities of the transitioning preset are met before cycling to the next one.", Indent),
            TooltipLine.Blank(),
            TooltipLine.Text("- Works with .cmrp and .crp", Indent),
            TooltipLine.Blank(),
            TooltipLine.Text("Preset Swap", Indent, bold: true),
            TooltipLine.Text("Each switch interval fully morphs to the next .cmrp preset, still one activity at a time separated by 3 seconds so that RP's transition rather than drop off and return as next preset.", Indent),
            TooltipLine.Blank(),
            TooltipLine.Text("- Only available for .cmrp", Indent),
            TooltipLine.Blank(),
            TooltipLine.Text("Match Order Compatibility", Indent, bold: true),
            TooltipLine.Text("Both cycle modes supports Match Order, it's a little on the side of an experimental feature for cycling RP's, requires specifics and should be used correctly.", Indent),
            TooltipLine.Blank(),
            TooltipLine.Text("Match Order must be enabled when Cycle RP's gets enabled, the Match Order state will apply the same across any incoming presets regardless of their Match Order state.", Indent),
            TooltipLine.Blank(),
            TooltipLine.Text("Difference between the modes in regard to Match Order:", Indent, bold: true),
            TooltipLine.Blank(),
            TooltipLine.Text("Slot Swap:", Indent, bold: true),
            TooltipLine.Text("The delay for Match Order isn't used since the switch interval acts as it, so it's recommended to set as time above 12 seconds to be safe on keeping order", Indent),
            TooltipLine.Text("Preset Swap:", Indent, bold: true),
            TooltipLine.Text("Uses Match Order delay. It's recommended to set Switch Interval time longer than the multiple of Match Order delay by the amount of Listening/Watching activities of the preset that has the most of them.", Indent),
            TooltipLine.Blank(),
        };

        public static TooltipLine[] GetCyclingActiveBannerLines() => new[]
        {
            TooltipLine.Blank(),
            TooltipLine.Text(ActivitiesUiText.CyclingPresetsActiveBannerTooltip, Indent),
            TooltipLine.Blank(),
        };

        public static TooltipLine[] GetCyclingPendingBannerLines() => new[]
        {
            TooltipLine.Blank(),
            TooltipLine.Text(
                IsMultiSlotPendingTooltip()
                    ? ActivitiesUiText.CyclingPresetsPendingBannerTooltip
                    : ActivitiesUiText.CyclingPresetsPendingBannerTooltipLegacy,
                Indent),
            TooltipLine.Blank(),
        };

        // Pending tooltip wording follows the current RP mode setting.
        static bool IsMultiSlotPendingTooltip()
        {
            int raw = Properties.Settings.Default.multiSlotRpcMode;
            // Match MainForm.GetMultiSlotRpcMode(): 1 (removed helper mode) counts as Multi-RP.
            return raw != (int)MultiSlotRpcMode.SingleProcess;
        }
    }
}
