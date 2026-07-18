using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace AtomicArt.Desktop.Controls.Generation;

public sealed class ImageDropMulticolorBorderControl : Control
{
    private const double BorderCornerRadius = 10d;
    private const double BorderThickness = 2d;
    private const double DashLength = 6d;
    private const double DashGapLength = 4d;
    private const double MiterLimit = 10d;

    private static readonly Color[] BorderColors =
    [
        Color.Parse("#55C7FF"),
        Color.Parse("#FF9B54"),
        Color.Parse("#FFD84D"),
        Color.Parse("#FF75B5"),
        Color.Parse("#C7A0FF"),
        Color.Parse("#FF5E68"),
        Color.Parse("#58D68D"),
        Color.Parse("#AAB3C5"),
        Color.Parse("#58D68D"),
        Color.Parse("#FF5E68"),
        Color.Parse("#C7A0FF"),
        Color.Parse("#FF75B5"),
        Color.Parse("#FFD84D"),
        Color.Parse("#FF9B54")
    ];
    private readonly Pen[] _borderPens;

    public ImageDropMulticolorBorderControl()
    {
        _borderPens = CreateBorderPens();
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

        foreach (Pen pen in _borderPens)
        {
            context.DrawRectangle(
                null,
                pen,
                borderBounds,
                BorderCornerRadius,
                BorderCornerRadius);
        }
    }

    private static Pen[] CreateBorderPens()
    {
        double dashStep = DashLength + DashGapLength;
        double cycleLength = dashStep * BorderColors.Length;
        double remainingCycleLength = cycleLength - DashLength;
        Pen[] pens = new Pen[BorderColors.Length];

        for (int index = 0; index < BorderColors.Length; index++)
        {
            double[] pattern = [DashLength, remainingCycleLength];
            DashStyle dashStyle = new(pattern, -index * dashStep);
            SolidColorBrush brush = new(BorderColors[index]);
            pens[index] = new Pen(
                brush,
                BorderThickness,
                dashStyle,
                PenLineCap.Round,
                PenLineJoin.Round,
                MiterLimit);
        }

        return pens;
    }
}
