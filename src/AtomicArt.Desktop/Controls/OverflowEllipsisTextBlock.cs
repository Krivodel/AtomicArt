using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;

namespace AtomicArt.Desktop.Controls;

public sealed class OverflowEllipsisTextBlock : TextBlock
{
    protected override Type StyleKeyOverride => typeof(TextBlock);

    private const string Ellipsis = "…";

    private static readonly TextTrimming OverflowTrimming =
        new LineAwareWordEllipsisTrimming();

    public OverflowEllipsisTextBlock()
    {
        TextTrimming = OverflowTrimming;
    }

    protected override TextLayout CreateTextLayout(string? text)
    {
        TextLayout textLayout = base.CreateTextLayout(text);

        if (string.IsNullOrEmpty(text))
        {
            return textLayout;
        }

        TextLine? lastLine = FindExplicitlyBrokenOverflowLine(text, textLayout);

        if (lastLine is null)
        {
            return textLayout;
        }

        int visibleLength = GetVisibleTextLength(text, lastLine);
        string visibleText = CreateEllipsizedText(text, visibleLength);

        textLayout.Dispose();

        return CreateUncachedTextLayout(visibleText);
    }

    private static TextLine? FindExplicitlyBrokenOverflowLine(
        string text,
        TextLayout textLayout)
    {
        IReadOnlyList<TextLine> textLines = textLayout.TextLines;

        if (textLines.Count == 0)
        {
            return null;
        }

        TextLine candidate = textLines[textLines.Count - 1];

        if (candidate.HasCollapsed
            || ((candidate.FirstTextSourceIndex + candidate.Length) >= text.Length)
            || (candidate.NewLineLength <= 0))
        {
            return null;
        }

        return candidate;
    }

    private static int GetVisibleTextLength(string text, TextLine lastLine)
    {
        int visibleLength =
            lastLine.FirstTextSourceIndex + lastLine.Length - lastLine.NewLineLength;
        int lineStart = Math.Clamp(lastLine.FirstTextSourceIndex, 0, text.Length);
        visibleLength = Math.Clamp(visibleLength, lineStart, text.Length);

        while (visibleLength > lineStart
               && char.IsWhiteSpace(text[visibleLength - 1]))
        {
            visibleLength--;
        }

        return visibleLength;
    }

    private static string CreateEllipsizedText(string text, int visibleLength)
    {
        ReadOnlySpan<char> visibleText = text.AsSpan(0, visibleLength);

        if (visibleText.EndsWith(Ellipsis, StringComparison.Ordinal))
        {
            return visibleText.ToString();
        }

        return string.Concat(visibleText, Ellipsis);
    }

    private TextLayout CreateUncachedTextLayout(string text)
    {
        Typeface typeface = new(
            FontFamily,
            FontStyle,
            FontWeight,
            FontStretch);
        Size maxSize = GetMaxSizeFromConstraint();

        return new TextLayout(
            text,
            typeface,
            fontSize: FontSize,
            foreground: Foreground,
            textAlignment: IsMeasureValid
                ? TextAlignment
                : TextAlignment.Left,
            textWrapping: TextWrapping,
            textTrimming: TextTrimming,
            textDecorations: TextDecorations,
            flowDirection: FlowDirection,
            maxWidth: maxSize.Width,
            maxHeight: maxSize.Height,
            lineHeight: LineHeight,
            letterSpacing: LetterSpacing,
            maxLines: MaxLines,
            fontFeatures: FontFeatures);
    }

    private sealed class LineAwareWordEllipsisTrimming : TextTrimming
    {
        public override TextCollapsingProperties CreateCollapsingProperties(
            TextCollapsingCreateInfo createInfo)
        {
            return new LineAwareWordEllipsisProperties(createInfo);
        }
    }

    private sealed class LineAwareWordEllipsisProperties :
        TextCollapsingProperties
    {
        public override double Width { get; }
        public override TextRun Symbol { get; }
        public override FlowDirection FlowDirection { get; }

        private readonly TextRunProperties _textRunProperties;

        public LineAwareWordEllipsisProperties(
            TextCollapsingCreateInfo createInfo)
        {
            Width = createInfo.Width;
            _textRunProperties = createInfo.TextRunProperties;
            FlowDirection = createInfo.FlowDirection;
            Symbol = new TextCharacters(Ellipsis, _textRunProperties);
        }

        public override TextRun[]? Collapse(TextLine textLine)
        {
            double collapseWidth = textLine.HasOverflowed
                ? Width
                : Math.Min(Width, textLine.WidthIncludingTrailingWhitespace);
            TextTrailingWordEllipsis wordEllipsis = new(
                Ellipsis,
                collapseWidth,
                _textRunProperties,
                FlowDirection);

            return wordEllipsis.Collapse(textLine);
        }
    }
}
