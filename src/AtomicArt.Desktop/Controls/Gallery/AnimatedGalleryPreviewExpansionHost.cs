using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace AtomicArt.Desktop.Controls.Gallery;

internal sealed class AnimatedGalleryPreviewExpansionHost : IGenerationPreviewExpansionHost
{
    public Control Viewport => _gallery.PreviewScrollViewer;
    public KeyModifiers CurrentKeyModifiers => _gallery.GetPreviewPointerModifiers();
    public Point? PointerPosition => _gallery.GetPreviewPointerPosition();

    public event EventHandler? PointerStateChanged
    {
        add => _gallery.PreviewPointerStateChanged += value;
        remove => _gallery.PreviewPointerStateChanged -= value;
    }

    private readonly AnimatedGalleryControl _gallery;

    public AnimatedGalleryPreviewExpansionHost(AnimatedGalleryControl gallery)
    {
        _gallery = gallery ?? throw new ArgumentNullException(nameof(gallery));
    }

    public void EnableOverflow(Control owner, Visual preview)
    {
        _gallery.EnablePreviewOverflow(owner, preview);
    }

    public void BeginOverflowCollapse(Control owner)
    {
        _gallery.BeginPreviewOverflowCollapse(owner);
    }

    public void DisableOverflow(Control owner)
    {
        _gallery.DisablePreviewOverflow(owner);
    }
}
