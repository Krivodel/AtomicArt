using AtomicArt.Desktop.Services.Gallery.State;

namespace AtomicArt.Desktop.Tests.TestDoubles;

internal sealed class RecordingGalleryStateService : IGalleryStateService
{
    private readonly GalleryState _state;

    public int SaveCallCount { get; private set; }
    public IReadOnlyList<GalleryItemState> SavedItems { get; private set; } =
        [];

    public RecordingGalleryStateService()
        : this(new GalleryState())
    {
    }

    public RecordingGalleryStateService(GalleryState state)
    {
        _state = state ?? throw new ArgumentNullException(nameof(state));
    }

    public Task<GalleryState> LoadAsync(CancellationToken ct)
    {
        return Task.FromResult(_state);
    }

    public Task SaveAsync(IReadOnlyList<GalleryItemState> items, CancellationToken ct)
    {
        SaveCallCount++;
        SavedItems = items.ToList();

        return Task.CompletedTask;
    }
}
