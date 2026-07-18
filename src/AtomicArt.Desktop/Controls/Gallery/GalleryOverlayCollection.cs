using Avalonia.Controls;

namespace AtomicArt.Desktop.Controls.Gallery;

internal static class GalleryOverlayCollection
{
    internal static void RemoveAll(
        Canvas overlayCanvas,
        IEnumerable<Control> overlays)
    {
        foreach (Control overlay in overlays)
        {
            overlayCanvas.Children.Remove(overlay);
        }
    }
}
