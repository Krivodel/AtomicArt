namespace AtomicArt.Desktop.Services.Gallery;

public sealed class GalleryDelegateImageViewerItemsSource : GalleryImageViewerItemsSource
{
    private readonly Func<IReadOnlyList<GalleryImageViewerItem>> _itemsFactory;

    public GalleryDelegateImageViewerItemsSource(Func<IReadOnlyList<GalleryImageViewerItem>> itemsFactory)
    {
        _itemsFactory = itemsFactory ?? throw new ArgumentNullException(nameof(itemsFactory));
    }

    public override IReadOnlyList<GalleryImageViewerItem> GetItems()
    {
        return _itemsFactory();
    }
}
