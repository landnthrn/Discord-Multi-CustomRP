namespace CustomRPC
{
    struct TooltipRun
    {
        public string Text;
        public bool Bold;
        public bool Italic;

        public TooltipRun(string text, bool bold = false, bool italic = false)
        {
            Text = text;
            Bold = bold;
            Italic = italic;
        }
    }

    sealed class TooltipLine
    {
        public bool IsBlank;
        public int IndentSpaces;
        public TooltipRun[] Runs;

        public static TooltipLine Blank() => new TooltipLine { IsBlank = true };

        public static TooltipLine Text(string text, int indentSpaces = 0, bool bold = false, bool italic = false) =>
            new TooltipLine
            {
                IndentSpaces = indentSpaces,
                Runs = new[] { new TooltipRun(text, bold, italic) },
            };

        public static TooltipLine WithRuns(int indentSpaces, params TooltipRun[] runs) =>
            new TooltipLine { IndentSpaces = indentSpaces, Runs = runs };
    }
}
