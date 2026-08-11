using Avalonia.Controls;

namespace AtomicArt.Desktop.Controls.Gallery;

internal interface IGalleryCardSurfaceProvider
{
    Control CardSurface { get; }
}
