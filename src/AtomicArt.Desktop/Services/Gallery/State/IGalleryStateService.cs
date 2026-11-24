namespace AtomicArt.Desktop.Services.Gallery.State;

public interface IGalleryStateService
{
    Task<GalleryState> LoadAsync(CancellationToken ct);

    Task SaveAsync(IReadOnlyList<GalleryItemState> items, CancellationToken ct);

    async Task SaveLatestAsync(Func<CancellationToken, Task<IReadOnlyList<GalleryItemState>>> getItems, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(getItems);

        IReadOnlyList<GalleryItemState> items = await getItems(ct).ConfigureAwait(false);
        await SaveAsync(items, ct).ConfigureAwait(false);
    }
}
