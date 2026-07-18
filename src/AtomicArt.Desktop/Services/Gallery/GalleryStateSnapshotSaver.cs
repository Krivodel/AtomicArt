using AtomicArt.Desktop.Services.Gallery.State;

namespace AtomicArt.Desktop.Services.Gallery;

internal static class GalleryStateSnapshotSaver
{
    public static async Task SaveAsync(
        IGalleryLifecycleViewState viewState,
        IGalleryStateService galleryStateService,
        Action<IReadOnlyList<GalleryItemState>>? stateSaved,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(viewState);
        ArgumentNullException.ThrowIfNull(galleryStateService);

        IReadOnlyList<GalleryItemState> snapshot = await viewState
            .CreateStateSnapshotAsync(ct)
            .ConfigureAwait(false);
        await galleryStateService.SaveAsync(snapshot, ct).ConfigureAwait(false);
        stateSaved?.Invoke(snapshot);
    }
}
