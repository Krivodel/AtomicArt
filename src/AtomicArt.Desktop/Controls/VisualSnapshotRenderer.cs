using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace AtomicArt.Desktop.Controls;

internal static class VisualSnapshotRenderer
{
    private const double DefaultDpi = 96d;

    public static RenderTargetBitmap? Capture(
        TopLevel topLevel,
        Control visual)
    {
        ArgumentNullException.ThrowIfNull(topLevel);
        ArgumentNullException.ThrowIfNull(visual);

        double renderScaling = topLevel.RenderScaling;
        if ((visual.Bounds.Width <= 0d)
            || (visual.Bounds.Height <= 0d)
            || !double.IsFinite(renderScaling)
            || (renderScaling <= 0d))
        {
            return null;
        }

        PixelSize pixelSize = PixelSize.FromSize(visual.Bounds.Size, renderScaling);
        Vector dpi = new(DefaultDpi * renderScaling, DefaultDpi * renderScaling);
        RenderTargetBitmap bitmap = new(pixelSize, dpi);
        bool isCompleted = false;

        try
        {
            using DrawingContext context = bitmap.CreateDrawingContext();
            VisualBrush visualBrush = new(visual);
            context.DrawRectangle(
                visualBrush,
                null,
                new Rect(visual.Bounds.Size));
            isCompleted = true;

            return bitmap;
        }
        finally
        {
            if (!isCompleted)
            {
                bitmap.Dispose();
            }
        }
    }
}
