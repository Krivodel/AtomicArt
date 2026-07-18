namespace AtomicArt.Desktop.Services.Gallery.State;

public interface IGalleryStateService
{
    Task<GalleryState> LoadAsync(CancellationToken ct);

    Task SaveAsync(IReadOnlyList<GalleryItemState> items, CancellationToken ct);
}
