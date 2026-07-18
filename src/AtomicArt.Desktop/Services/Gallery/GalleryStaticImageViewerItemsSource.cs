namespace AtomicArt.Desktop.Services.Gallery;

public sealed class GalleryStaticImageViewerItemsSource : GalleryImageViewerItemsSource
{
    private readonly IReadOnlyList<GalleryImageViewerItem> _items;

    public GalleryStaticImageViewerItemsSource(IReadOnlyList<GalleryImageViewerItem> items)
    {
        _items = items ?? throw new ArgumentNullException(nameof(items));
    }

    public override IReadOnlyList<GalleryImageViewerItem> GetItems()
    {
        return _items;
    }
}
