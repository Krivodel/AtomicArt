using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.VisualTree;

using FluentAssertions;

namespace AtomicArt.Desktop.Tests.Common;

internal static class TextBoxScrollContentAssertions
{
    internal static Control GetTextPresenter(TextBox textBox)
    {
        ArgumentNullException.ThrowIfNull(textBox);

        return textBox
            .GetVisualDescendants()
            .OfType<Control>()
            .Single(control => string.Equals(
                control.Name,
                "PART_TextPresenter",
                StringComparison.Ordinal));
    }

    internal static void AssertUsesScrollableInsets(
        TextBox textBox,
        Window window,
        Thickness expectedMargin)
    {
        ArgumentNullException.ThrowIfNull(textBox);
        ArgumentNullException.ThrowIfNull(window);

        ScrollViewer scrollViewer = textBox
            .GetVisualDescendants()
            .OfType<ScrollViewer>()
            .Single();
        Control textPresenter = GetTextPresenter(textBox);
        Rect scrollViewportBounds = GetTransformedBounds(scrollViewer, textBox);
        Rect initialTextBounds = GetTransformedBounds(textPresenter, textBox);

        textBox.Padding.Should().Be(new Thickness(0d));
        textPresenter.Margin.Should().Be(expectedMargin);
        (initialTextBounds.Top - scrollViewportBounds.Top)
            .Should()
            .BeApproximately(expectedMargin.Top, 0.01d);

        scrollViewer.Offset = new Vector(0d, expectedMargin.Top);
        window.CaptureRenderedFrame();
        Rect topEdgeTextBounds = GetTransformedBounds(textPresenter, textBox);

        scrollViewer.Offset.Y.Should().BeApproximately(expectedMargin.Top, 0.01d);
        topEdgeTextBounds.Top.Should()
            .BeApproximately(scrollViewportBounds.Top, 0.01d);

        double bottomEdgeOffset = scrollViewer.Extent.Height
            - scrollViewer.Viewport.Height
            - expectedMargin.Bottom;
        bottomEdgeOffset.Should().BeGreaterThan(0d);

        scrollViewer.Offset = new Vector(0d, bottomEdgeOffset);
        window.CaptureRenderedFrame();
        Rect bottomEdgeTextBounds = GetTransformedBounds(textPresenter, textBox);

        bottomEdgeTextBounds.Bottom.Should()
            .BeApproximately(scrollViewportBounds.Bottom, 0.01d);
    }

    private static Rect GetTransformedBounds(Control control, Visual target)
    {
        Matrix transform = control.TransformToVisual(target)
            ?? throw new InvalidOperationException("Control transform was not found.");

        return new Rect(control.Bounds.Size).TransformToAABB(transform);
    }
}
