using AtomicArt.Desktop.Services.Gallery.State;

namespace AtomicArt.Desktop.Tests.TestDoubles;

internal sealed class NoOpGalleryFileOrderSynchronizer
    : IGalleryFileOrderSynchronizer
{
    public Task SynchronizeAsync(
        IReadOnlyList<GalleryItemState> items,
        CancellationToken ct)
    {
        return Task.CompletedTask;
    }
}
