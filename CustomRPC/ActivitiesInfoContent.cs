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
            TooltipLine.Text("The delay is to ensure they're individually connected at the right times. ", MatchIndent),
            TooltipLine.Text("(Recommended 12(+) second delay) ", MatchIndent),
            TooltipLine.Text("Drag & drop to arrange order of slots. ", MatchIndent),
            TooltipLine.Blank(),
        };
    }
}
