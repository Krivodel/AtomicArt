namespace AtomicArt.Desktop.Services.Gallery;

internal sealed class RemoveGalleryOperation : GalleryOperation
{
    internal RemoveGalleryOperation(Guid itemId)
        : base([], itemId)
    {
    }
}
