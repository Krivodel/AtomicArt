using Avalonia;
using Avalonia.Controls;

namespace AtomicArt.Desktop.Views.Generation;

internal sealed class GenerationChatMessagePanel : Panel
{
    public double EditorProgress
    {
        get => GetValue(EditorProgressProperty);
        set => SetValue(EditorProgressProperty, value);
    }
    public bool StretchBubble
    {
        get => GetValue(StretchBubbleProperty);
        set => SetValue(StretchBubbleProperty, value);
    }

    public static readonly StyledProperty<double> EditorProgressProperty =
        AvaloniaProperty.Register<GenerationChatMessagePanel, double>(nameof(EditorProgress));
    public static readonly StyledProperty<bool> StretchBubbleProperty =
        AvaloniaProperty.Register<GenerationChatMessagePanel, bool>(nameof(StretchBubble));

    static GenerationChatMessagePanel()
    {
        AffectsMeasure<GenerationChatMessagePanel>(EditorProgressProperty, StretchBubbleProperty);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        if (Children.Count != 3)
        {
            return default;
        }

        Control bubble = Children[0];
        Control tools = Children[1];
        Control editorActions = Children[2];
        editorActions.Measure(new Size(availableSize.Width, double.PositiveInfinity));
        bubble.Measure(availableSize);
        tools.Measure(availableSize);
        double width = StretchBubble && double.IsFinite(availableSize.Width)
            ? availableSize.Width
            : Math.Min(availableSize.Width, Math.Max(editorActions.DesiredSize.Width,
                Math.Max(bubble.DesiredSize.Width, tools.DesiredSize.Width)));
        double footerHeight = tools.DesiredSize.Height
            + (Math.Max(0d, editorActions.DesiredSize.Height - tools.DesiredSize.Height) * EditorProgress);

        return new Size(width, bubble.DesiredSize.Height + footerHeight);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (Children.Count == 3)
        {
            double height = Children[0].DesiredSize.Height;
            Children[0].Arrange(new Rect(0d, 0d, finalSize.Width, height));
            Children[1].Arrange(new Rect(0d, height, finalSize.Width, Children[1].DesiredSize.Height));
            double editorWidth = Math.Max(finalSize.Width, Children[2].DesiredSize.Width);
            Children[2].Arrange(new Rect(finalSize.Width - editorWidth, height, editorWidth, Children[2].DesiredSize.Height));
        }

        return finalSize;
    }
}
