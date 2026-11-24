using AtomicArt.Desktop.Services.Gallery.State;

namespace AtomicArt.Desktop.Services.Gallery;

internal static class GalleryStateSnapshotSaver
{
    public static Task SaveAsync(
        IGalleryLifecycleViewState viewState,
        IGalleryStateService galleryStateService,
        Action<IReadOnlyList<GalleryItemState>>? stateSaved,
        CancellationToken ct)
    {
        return SaveCoreAsync(
            viewState,
            galleryStateService,
            fileOrderSynchronizer: null,
            stateSaved,
            ct);
    }

    public static Task SynchronizeFilesAndSaveAsync(
        IGalleryLifecycleViewState viewState,
        IGalleryStateService galleryStateService,
        IGalleryFileOrderSynchronizer fileOrderSynchronizer,
        Action<IReadOnlyList<GalleryItemState>>? stateSaved,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(fileOrderSynchronizer);

        return SaveCoreAsync(
            viewState,
            galleryStateService,
            fileOrderSynchronizer,
            stateSaved,
            ct);
    }

    private static async Task SaveCoreAsync(
        IGalleryLifecycleViewState viewState,
        IGalleryStateService galleryStateService,
        IGalleryFileOrderSynchronizer? fileOrderSynchronizer,
        Action<IReadOnlyList<GalleryItemState>>? stateSaved,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(viewState);
        ArgumentNullException.ThrowIfNull(galleryStateService);

        if (fileOrderSynchronizer is not null)
        {
            IReadOnlyList<GalleryItemState> order = await viewState.CreateStateSnapshotAsync(ct).ConfigureAwait(false);
            await fileOrderSynchronizer
                .SynchronizeAsync(order, ct)
                .ConfigureAwait(false);
        }

        IReadOnlyList<GalleryItemState> snapshot = [];
        await galleryStateService.SaveLatestAsync(async token =>
        {
            snapshot = await viewState.CreateStateSnapshotAsync(token).ConfigureAwait(false);

            return snapshot;
        }, ct).ConfigureAwait(false);
        stateSaved?.Invoke(snapshot);
    }
}
