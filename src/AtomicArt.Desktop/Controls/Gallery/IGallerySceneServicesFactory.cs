using Avalonia.Controls;

namespace AtomicArt.Desktop.Controls.Gallery;

internal interface IGallerySceneServicesFactory
{
    AnimatedGalleryScene Create(TopLevel topLevel);
}
