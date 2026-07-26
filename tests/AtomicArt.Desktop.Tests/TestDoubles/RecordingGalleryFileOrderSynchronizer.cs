using AtomicArt.Desktop.Services.Gallery.State;

namespace AtomicArt.Desktop.Tests.TestDoubles;

internal sealed class RecordingGalleryFileOrderSynchronizer
    : IGalleryFileOrderSynchronizer
{
    public int CallCount { get; private set; }
    public IReadOnlyList<GalleryItemState> Items { get; private set; } = [];

    public Task SynchronizeAsync(
        IReadOnlyList<GalleryItemState> items,
        CancellationToken ct)
    {
        CallCount++;
        Items = items.ToList();

        return Task.CompletedTask;
    }
}
