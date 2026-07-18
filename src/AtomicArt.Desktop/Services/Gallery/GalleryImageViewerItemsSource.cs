namespace AtomicArt.Desktop.Services.Gallery;

public abstract class GalleryImageViewerItemsSource
{
    public abstract IReadOnlyList<GalleryImageViewerItem> GetItems();
}
