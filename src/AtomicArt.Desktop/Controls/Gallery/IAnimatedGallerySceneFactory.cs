using Avalonia.Controls;

namespace AtomicArt.Desktop.Controls.Gallery;

internal interface IAnimatedGallerySceneFactory
{
    AnimatedGalleryScene Create(TopLevel topLevel);
}
