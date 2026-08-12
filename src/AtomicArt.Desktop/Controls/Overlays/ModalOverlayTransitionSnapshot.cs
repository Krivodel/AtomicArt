using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;

namespace AtomicArt.Desktop.Controls.Overlays;

internal sealed class ModalOverlayTransitionSnapshot : IDisposable
{
    private readonly Border _targetHost;
    private readonly Image _targetImage;
    private readonly BlurBackdropControl _targetBlur;
    private RenderTargetBitmap? _panelBitmap;

    private ModalOverlayTransitionSnapshot(
        Border targetHost,
        Border targetClip,
        Image targetImage,
        BlurBackdropControl targetBlur,
        RenderTargetBitmap panelBitmap,
        Rect panelBounds,
        CornerRadius cornerRadius,
        double blurRadius,
        bool isBlurDynamic)
    {
        _targetHost = targetHost;
        _targetImage = targetImage;
        _targetBlur = targetBlur;
        _panelBitmap = panelBitmap;
        _targetHost.Width = panelBounds.Width;
        _targetHost.Height = panelBounds.Height;
        _targetHost.Margin = new Thickness(panelBounds.X, panelBounds.Y, 0d, 0d);
        _targetHost.CornerRadius = cornerRadius;
        targetClip.CornerRadius = cornerRadius;
        _targetBlur.BlurRadius = blurRadius;
        _targetBlur.IsDynamic = isBlurDynamic;
        _targetImage.Source = panelBitmap;
        _targetHost.IsVisible = true;
    }

    public static ModalOverlayTransitionSnapshot? Create(
        TopLevel topLevel,
        ModalOverlayControl panel,
        Control targetCoordinateSpace,
        Border targetHost,
        Border targetClip,
        Image targetImage,
        BlurBackdropControl targetBlur)
    {
        ArgumentNullException.ThrowIfNull(topLevel);
        ArgumentNullException.ThrowIfNull(panel);
        ArgumentNullException.ThrowIfNull(targetCoordinateSpace);
        ArgumentNullException.ThrowIfNull(targetHost);
        ArgumentNullException.ThrowIfNull(targetClip);
        ArgumentNullException.ThrowIfNull(targetImage);
        ArgumentNullException.ThrowIfNull(targetBlur);

        Point panelBottomRight = new(panel.Bounds.Width, panel.Bounds.Height);
        Point? panelTargetStart = panel.TranslatePoint(new Point(), targetCoordinateSpace);
        Point? panelTargetEnd = panel.TranslatePoint(panelBottomRight, targetCoordinateSpace);
        if (panelTargetStart is null
            || panelTargetEnd is null
            || (panel.Bounds.Width <= 0d)
            || (panel.Bounds.Height <= 0d))
        {
            return null;
        }

        Rect panelTargetBounds = CreateRect(panelTargetStart.Value, panelTargetEnd.Value);
        BlurBackdropControl? panelBlur = panel.BlurBackdrop;
        bool wasBlurVisible = panelBlur?.IsVisible ?? false;
        RenderTargetBitmap? panelBitmap = null;
        bool isCompleted = false;

        try
        {
            if (panelBlur is not null)
            {
                panelBlur.IsVisible = false;
            }

            panelBitmap = VisualSnapshotRenderer.Capture(topLevel, panel);
            if (panelBitmap is null)
            {
                return null;
            }

            ModalOverlayTransitionSnapshot snapshot = new(
                targetHost,
                targetClip,
                targetImage,
                targetBlur,
                panelBitmap,
                panelTargetBounds,
                panel.CornerRadius,
                panel.BlurRadius,
                panel.IsBlurDynamic);
            isCompleted = true;

            return snapshot;
        }
        finally
        {
            if (panelBlur is not null)
            {
                panelBlur.IsVisible = wasBlurVisible;
            }

            if (!isCompleted)
            {
                panelBitmap?.Dispose();
            }
        }
    }

    public void Dispose()
    {
        _targetImage.Source = null;
        _targetBlur.BlurRadius = 0d;
        _targetBlur.IsDynamic = false;
        _targetHost.IsVisible = false;
        _panelBitmap?.Dispose();
        _panelBitmap = null;
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
