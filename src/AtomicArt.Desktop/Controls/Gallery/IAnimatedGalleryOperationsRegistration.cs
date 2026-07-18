using AtomicArt.Desktop.Services.Gallery;

namespace AtomicArt.Desktop.Controls.Gallery;

internal interface IAnimatedGalleryOperationsRegistration
{
    void Attach(IAnimatedGalleryOperations operations);

    void Detach(IAnimatedGalleryOperations operations);
}
