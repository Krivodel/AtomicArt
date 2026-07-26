namespace AtomicArt.Desktop.Services.Gallery.State;

public interface IGalleryFileOrderSynchronizer
{
    Task SynchronizeAsync(
        IReadOnlyList<GalleryItemState> items,
        CancellationToken ct);
}
