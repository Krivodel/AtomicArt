namespace AtomicArt.Desktop.Services.Gallery.State;

public sealed class GalleryState
{
    public IReadOnlyList<GalleryItemState> Items { get; init; } = [];
}
