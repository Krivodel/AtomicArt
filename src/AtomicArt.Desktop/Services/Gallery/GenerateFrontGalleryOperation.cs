namespace AtomicArt.Desktop.Services.Gallery;

internal sealed class GenerateFrontGalleryOperation : GalleryOperation
{
    internal GenerateFrontGalleryOperation(IReadOnlyList<object> items)
        : base(items, null)
    {
    }
}
