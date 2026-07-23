using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;

namespace AtomicArt.Desktop.Controls.Overlays;

internal sealed class ModalOverlayTransitionSnapshot : IDisposable
{
    private const double DefaultDpi = 96d;

    private readonly Border _targetHost;
    private readonly Image _targetImage;
    private CroppedBitmap? _croppedBitmap;
    private RenderTargetBitmap? _windowBitmap;

    private ModalOverlayTransitionSnapshot(
        Border targetHost,
        Border targetClip,
        Image targetImage,
        RenderTargetBitmap windowBitmap,
        CroppedBitmap croppedBitmap,
        Rect panelBounds,
        CornerRadius cornerRadius)
    {
        _targetHost = targetHost;
        _targetImage = targetImage;
        _windowBitmap = windowBitmap;
        _croppedBitmap = croppedBitmap;
        _targetHost.Width = panelBounds.Width;
        _targetHost.Height = panelBounds.Height;
        _targetHost.Margin = new Thickness(panelBounds.X, panelBounds.Y, 0d, 0d);
        _targetHost.CornerRadius = cornerRadius;
        targetClip.CornerRadius = cornerRadius;
        _targetImage.Source = croppedBitmap;
        _targetHost.IsVisible = true;
    }

    public static ModalOverlayTransitionSnapshot? Create(
        TopLevel topLevel,
        ModalOverlayControl panel,
        Control targetCoordinateSpace,
        Border targetHost,
        Border targetClip,
        Image targetImage)
    {
        ArgumentNullException.ThrowIfNull(topLevel);
        ArgumentNullException.ThrowIfNull(panel);
        ArgumentNullException.ThrowIfNull(targetCoordinateSpace);
        ArgumentNullException.ThrowIfNull(targetHost);
        ArgumentNullException.ThrowIfNull(targetClip);
        ArgumentNullException.ThrowIfNull(targetImage);

        double renderScaling = topLevel.RenderScaling;
        Point panelBottomRight = new(panel.Bounds.Width, panel.Bounds.Height);
        Point? panelTopLevelStart = panel.TranslatePoint(new Point(), topLevel);
        Point? panelTopLevelEnd = panel.TranslatePoint(panelBottomRight, topLevel);
        Point? panelTargetStart = panel.TranslatePoint(new Point(), targetCoordinateSpace);
        Point? panelTargetEnd = panel.TranslatePoint(panelBottomRight, targetCoordinateSpace);
        Rect topLevelBounds = topLevel.Bounds;
        if (panelTopLevelStart is null
            || panelTopLevelEnd is null
            || panelTargetStart is null
            || panelTargetEnd is null
            || (panel.Bounds.Width <= 0d)
            || (panel.Bounds.Height <= 0d)
            || (topLevelBounds.Width <= 0d)
            || (topLevelBounds.Height <= 0d)
            || !double.IsFinite(renderScaling)
            || (renderScaling <= 0d))
        {
            return null;
        }

        Rect panelTopLevelBounds = CreateRect(panelTopLevelStart.Value, panelTopLevelEnd.Value);
        Rect panelTargetBounds = CreateRect(panelTargetStart.Value, panelTargetEnd.Value);
        PixelSize pixelSize = PixelSize.FromSize(topLevelBounds.Size, renderScaling);
        PixelRect? sourceRect = CreateSourceRect(
            panelTopLevelBounds,
            pixelSize,
            renderScaling);
        if (sourceRect is null)
        {
            return null;
        }

        Vector dpi = new(DefaultDpi * renderScaling, DefaultDpi * renderScaling);
        RenderTargetBitmap windowBitmap = new(pixelSize, dpi);
        CroppedBitmap? croppedBitmap = null;
        bool isCompleted = false;

        try
        {
            windowBitmap.Render(topLevel);
            croppedBitmap = new CroppedBitmap(windowBitmap, sourceRect.Value);
            ModalOverlayTransitionSnapshot snapshot = new(
                targetHost,
                targetClip,
                targetImage,
                windowBitmap,
                croppedBitmap,
                panelTargetBounds,
                panel.CornerRadius);
            isCompleted = true;

            return snapshot;
        }
        finally
        {
            if (!isCompleted)
            {
                if (croppedBitmap is not null)
                {
                    croppedBitmap.Dispose();
                }
                else
                {
                    windowBitmap.Dispose();
                }
            }
        }
    }

    public void Dispose()
    {
        _targetImage.Source = null;
        _targetHost.IsVisible = false;
        if (_croppedBitmap is not null)
        {
            _croppedBitmap.Dispose();
        }
        else
        {
            _windowBitmap?.Dispose();
        }

        _croppedBitmap = null;
        _windowBitmap = null;
    }

    private static PixelRect? CreateSourceRect(
        Rect panelBounds,
        PixelSize sourceSize,
        double renderScaling)
    {
        int left = Math.Clamp(
            (int)Math.Floor(panelBounds.Left * renderScaling),
            0,
            sourceSize.Width);
        int top = Math.Clamp(
            (int)Math.Floor(panelBounds.Top * renderScaling),
            0,
            sourceSize.Height);
        int right = Math.Clamp(
            (int)Math.Ceiling(panelBounds.Right * renderScaling),
            0,
            sourceSize.Width);
        int bottom = Math.Clamp(
            (int)Math.Ceiling(panelBounds.Bottom * renderScaling),
            0,
            sourceSize.Height);

        return (right <= left) || (bottom <= top)
            ? null
            : new PixelRect(left, top, right - left, bottom - top);
    }

    private static Rect CreateRect(Point first, Point second)
    {
        double left = Math.Min(first.X, second.X);
        double top = Math.Min(first.Y, second.Y);
        double right = Math.Max(first.X, second.X);
        double bottom = Math.Max(first.Y, second.Y);

        return new Rect(left, top, right - left, bottom - top);
    }
}
