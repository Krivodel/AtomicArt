namespace AtomicArt.Desktop.Services.Gallery;

internal sealed class RevealGalleryItemOperation : GalleryOperation
{
    internal RevealGalleryItemOperation(Guid itemId)
        : base(new List<object>(), itemId)
    {
    }
}
