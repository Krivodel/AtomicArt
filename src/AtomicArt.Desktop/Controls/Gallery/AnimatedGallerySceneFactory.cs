using Avalonia.Controls;

namespace AtomicArt.Desktop.Controls.Gallery;

internal sealed class AnimatedGallerySceneFactory : IAnimatedGallerySceneFactory
{
    private readonly IGallerySceneServicesFactory _sceneServicesFactory;

    public AnimatedGallerySceneFactory(IGallerySceneServicesFactory sceneServicesFactory)
    {
        _sceneServicesFactory = sceneServicesFactory ?? throw new ArgumentNullException(nameof(sceneServicesFactory));
    }

    public AnimatedGalleryScene Create(TopLevel topLevel)
    {
        ArgumentNullException.ThrowIfNull(topLevel);

        return _sceneServicesFactory.Create(topLevel);
    }
}
