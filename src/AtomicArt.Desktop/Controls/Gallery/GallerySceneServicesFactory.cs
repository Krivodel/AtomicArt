using Avalonia.Controls;

namespace AtomicArt.Desktop.Controls.Gallery;

internal sealed class GallerySceneServicesFactory : IGallerySceneServicesFactory
{
    private readonly Func<TopLevel, AnimatedGalleryScene> _createScene;

    public GallerySceneServicesFactory(Func<TopLevel, AnimatedGalleryScene> createScene)
    {
        _createScene = createScene ?? throw new ArgumentNullException(nameof(createScene));
    }

    public AnimatedGalleryScene Create(TopLevel topLevel)
    {
        ArgumentNullException.ThrowIfNull(topLevel);

        return _createScene(topLevel);
    }
}
