using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace AtomicArt.Desktop.Controls.Gallery;

public sealed class GallerySelectionOutlineControl : Control
{
    public IBrush? Stroke
    {
        get => GetValue(StrokeProperty);
        set => SetValue(StrokeProperty, value);
    }
    public double StrokeThickness
    {
        get => GetValue(StrokeThicknessProperty);
        set => SetValue(StrokeThicknessProperty, value);
    }

    public static readonly StyledProperty<IBrush?> StrokeProperty =
        AvaloniaProperty.Register<GallerySelectionOutlineControl, IBrush?>(
            nameof(Stroke));
    public static readonly StyledProperty<double> StrokeThicknessProperty =
        AvaloniaProperty.Register<GallerySelectionOutlineControl, double>(
            nameof(StrokeThickness));

    private const double MiterLimit = 10d;

    static GallerySelectionOutlineControl()
    {
        AffectsRender<GallerySelectionOutlineControl>(
            StrokeProperty,
            StrokeThicknessProperty);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        double strokeThickness = NormalizeStrokeThickness(StrokeThickness);
        IBrush? stroke = Stroke;
        if ((stroke is null)
            || (strokeThickness <= 0d)
            || (Bounds.Width <= 0d)
            || (Bounds.Height <= 0d))
        {
            return;
        }

        Rect strokeCenterBounds = CalculateStrokeCenterBounds(
            Bounds.Size,
            strokeThickness);
        Pen pen = new(
            stroke,
            strokeThickness,
            null,
            PenLineCap.Square,
            PenLineJoin.Miter,
            MiterLimit);

        context.DrawRectangle(null, pen, strokeCenterBounds);
    }

    internal static Rect CalculateStrokeCenterBounds(
        Size controlSize,
        double strokeThickness)
    {
        double halfStrokeThickness = strokeThickness / 2d;

        return new Rect(
            halfStrokeThickness,
            halfStrokeThickness,
            Math.Max(0d, controlSize.Width - strokeThickness),
            Math.Max(0d, controlSize.Height - strokeThickness));
    }

    private static double NormalizeStrokeThickness(double strokeThickness)
    {
        return double.IsFinite(strokeThickness)
            ? Math.Max(0d, strokeThickness)
            : 0d;
    }
}
