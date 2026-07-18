namespace AtomicArt.Desktop.Services.Gallery;

internal sealed class AppendBatchGalleryOperation : GalleryOperation
{
    internal AppendBatchGalleryOperation(IReadOnlyList<object> items)
        : base(items, null)
    {
    }
}
