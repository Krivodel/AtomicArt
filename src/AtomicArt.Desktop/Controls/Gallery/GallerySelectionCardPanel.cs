using Avalonia;
using Avalonia.Controls;

namespace AtomicArt.Desktop.Controls.Gallery;

public sealed class GallerySelectionCardPanel : Panel
{
    public double OutlineThickness
    {
        get => GetValue(OutlineThicknessProperty);
        set => SetValue(OutlineThicknessProperty, value);
    }

    public static readonly StyledProperty<double> OutlineThicknessProperty =
        AvaloniaProperty.Register<GallerySelectionCardPanel, double>(
            nameof(OutlineThickness));

    static GallerySelectionCardPanel()
    {
        AffectsArrange<GallerySelectionCardPanel>(OutlineThicknessProperty);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        Size desiredSize = default;

        foreach (Control child in Children)
        {
            child.Measure(availableSize);
            if (child is GallerySelectionOutlineControl)
            {
                continue;
            }

            desiredSize = new Size(
                Math.Max(desiredSize.Width, child.DesiredSize.Width),
                Math.Max(desiredSize.Height, child.DesiredSize.Height));
        }

        return desiredSize;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        double outlineThickness = NormalizeOutlineThickness(OutlineThickness);
        Rect cardBounds = CalculateCardBounds(finalSize, outlineThickness);
        Rect outlineBounds = CalculateOutlineBounds(finalSize, outlineThickness);

        foreach (Control child in Children)
        {
            child.Arrange(child is GallerySelectionOutlineControl
                ? outlineBounds
                : cardBounds);
        }

        return finalSize;
    }

    private static Rect CalculateCardBounds(
        Size panelSize,
        double outlineThickness)
    {
        return new Rect(
            outlineThickness,
            outlineThickness,
            panelSize.Width,
            panelSize.Height);
    }

    private static Rect CalculateOutlineBounds(
        Size panelSize,
        double outlineThickness)
    {
        double outlineExpansion = outlineThickness * 2d;

        return new Rect(
            0d,
            0d,
            panelSize.Width + outlineExpansion,
            panelSize.Height + outlineExpansion);
    }

    private static double NormalizeOutlineThickness(double outlineThickness)
    {
        return double.IsFinite(outlineThickness)
            ? Math.Max(0d, outlineThickness)
            : 0d;
    }
}
