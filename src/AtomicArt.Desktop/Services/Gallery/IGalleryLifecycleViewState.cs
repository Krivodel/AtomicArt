using AtomicArt.Desktop.Services.Gallery.State;

namespace AtomicArt.Desktop.Services.Gallery;

public interface IGalleryLifecycleViewState
{
    Task ApplyStartedAsync(GenerationLifecycleEvent lifecycleEvent, CancellationToken ct);
    Task ApplyCompletedAsync(
        Guid correlationId,
        IReadOnlyList<GalleryCompletedItemUpdate> itemUpdates,
        CancellationToken ct);
    Task ApplyStartFailedAsync(Guid correlationId, CancellationToken ct);
    Task ApplyFailedAsync(Guid correlationId, CancellationToken ct);
    Task RefreshElapsedTextAsync(DateTime utcNow, CancellationToken ct);
    Task<IReadOnlyList<GalleryItemState>> CreateStateSnapshotAsync(CancellationToken ct);
    Task RestoreAsync(IReadOnlyList<GalleryItemState> items, CancellationToken ct);
}
