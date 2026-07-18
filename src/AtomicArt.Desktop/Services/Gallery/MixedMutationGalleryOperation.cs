namespace AtomicArt.Desktop.Services.Gallery;

internal sealed class MixedMutationGalleryOperation : GalleryOperation
{
    internal MixedMutationGalleryOperation(IReadOnlyList<object> items)
        : base(items, null)
    {
    }
}
