using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace AtomicArt.Desktop.Controls.Generation;

public sealed class ImageDropDashedBorderControl : Control
{
    public Color StrokeColor
    {
        get => GetValue(StrokeColorProperty);
        set => SetValue(StrokeColorProperty, value);
    }

    private const double BorderCornerRadius = 10d;
    private const double BorderThickness = 2d;
    private const double DashLength = 6d;
    private const double DashGapLength = 4d;
    private const double MiterLimit = 10d;

    public static readonly StyledProperty<Color> StrokeColorProperty =
        AvaloniaProperty.Register<ImageDropDashedBorderControl, Color>(
            nameof(StrokeColor),
            defaultValue: Colors.DodgerBlue);

    static ImageDropDashedBorderControl()
    {
        AffectsRender<ImageDropDashedBorderControl>(StrokeColorProperty);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        if (Bounds.Width <= BorderThickness || Bounds.Height <= BorderThickness)
        {
            return;
        }

        double inset = BorderThickness / 2d;
        Rect borderBounds = new(
            inset,
            inset,
            Bounds.Width - BorderThickness,
            Bounds.Height - BorderThickness);

        context.DrawRectangle(
            null,
            CreateBorderPen(),
            borderBounds,
            BorderCornerRadius,
            BorderCornerRadius);
    }

    private Pen CreateBorderPen()
    {
        double[] pattern = [DashLength, DashGapLength];
        DashStyle dashStyle = new(pattern, 0d);
        SolidColorBrush brush = new(StrokeColor);

        return new Pen(
            brush,
            BorderThickness,
            dashStyle,
            PenLineCap.Round,
            PenLineJoin.Round,
            MiterLimit);
    }
}
