using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace CustomRPC
{
    static class TooltipPainter
    {
        const TextFormatFlags TextMeasureFlags = TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix;
        const TextFormatFlags TextDrawFlags = TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix;

        sealed class DrawSegment
        {
            public string Text;
            public Font Font;
            public float X;
            public float Y;
            public float Width;
        }

        public static Size Layout(Graphics g, TooltipLine[] lines, Font baseFont, Padding padding, int maxContentWidth)
        {
            var segments = BuildSegments(g, lines, baseFont, padding, maxContentWidth);
            if (segments.Count == 0)
                return new Size(padding.Horizontal + 40, padding.Vertical + baseFont.Height);

            float maxRight = 0;
            float maxBottom = 0;
            foreach (var segment in segments)
            {
                maxRight = Math.Max(maxRight, segment.X + segment.Width);
                maxBottom = Math.Max(maxBottom, segment.Y + segment.Font.Height);
            }

            int width = (int)Math.Ceiling(maxRight) + padding.Right;
            int height = (int)Math.Ceiling(maxBottom) + padding.Bottom;
            return new Size(width, height);
        }

        public static void Paint(Graphics g, TooltipLine[] lines, Font baseFont, Padding padding, int maxContentWidth)
        {
            using (var background = new SolidBrush(DarkToolTipHelper.Background))
                g.FillRectangle(background, 0, 0, g.VisibleClipBounds.Width, g.VisibleClipBounds.Height);

            foreach (var segment in BuildSegments(g, lines, baseFont, padding, maxContentWidth))
            {
                TextRenderer.DrawText(
                    g,
                    segment.Text,
                    segment.Font,
                    new Point((int)segment.X, (int)segment.Y),
                    DarkToolTipHelper.Foreground,
                    TextDrawFlags);
            }
        }

        static List<DrawSegment> BuildSegments(Graphics g, TooltipLine[] lines, Font baseFont, Padding padding, int maxContentWidth)
        {
            var segments = new List<DrawSegment>();
            float y = padding.Top;
            int spaceWidth = TextRenderer.MeasureText(g, " ", baseFont, new Size(int.MaxValue, int.MaxValue), TextMeasureFlags).Width;
            int contentRight = padding.Left + maxContentWidth;

            foreach (var line in lines)
            {
                if (line.IsBlank)
                {
                    y += baseFont.Height / 2f;
                    continue;
                }

                int indentPx = padding.Left + line.IndentSpaces * spaceWidth;
                float x = indentPx;
                float lineHeight = baseFont.Height;

                foreach (var run in line.Runs)
                {
                    if (string.IsNullOrEmpty(run.Text))
                        continue;

                    Font runFont = GetRunFont(baseFont, run);
                    lineHeight = Math.Max(lineHeight, runFont.Height);

                    foreach (string word in SplitWords(run.Text))
                    {
                        int wordWidth = TextRenderer.MeasureText(g, word, runFont, new Size(int.MaxValue, int.MaxValue), TextMeasureFlags).Width;
                        if (x > indentPx && x + wordWidth > contentRight)
                        {
                            y += lineHeight;
                            x = indentPx;
                            lineHeight = runFont.Height;
                        }

                        segments.Add(new DrawSegment
                        {
                            Text = word,
                            Font = runFont,
                            X = x,
                            Y = y,
                            Width = wordWidth,
                        });

                        x += wordWidth;
                    }
                }

                y += lineHeight;
            }

            return segments;
        }

        static IEnumerable<string> SplitWords(string text)
        {
            if (string.IsNullOrEmpty(text))
                yield break;

            int start = 0;
            for (int i = 0; i < text.Length; i++)
            {
                if (!char.IsWhiteSpace(text[i]))
                    continue;

                if (i > start)
                    yield return text.Substring(start, i - start);

                yield return text.Substring(i, 1);
                start = i + 1;
            }

            if (start < text.Length)
                yield return text.Substring(start);
        }

        static Font GetRunFont(Font baseFont, TooltipRun run)
        {
            FontStyle style = FontStyle.Regular;
            if (run.Bold)
                style |= FontStyle.Bold;
            if (run.Italic)
                style |= FontStyle.Italic;
            return style == baseFont.Style ? baseFont : new Font(baseFont, style);
        }
    }
}
