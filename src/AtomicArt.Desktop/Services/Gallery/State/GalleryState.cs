using System.Text.Json.Serialization;

namespace AtomicArt.Desktop.Services.Gallery.State;

public sealed class GalleryState
{
    [JsonRequired]
    public IReadOnlyList<GalleryItemState> Items { get; init; } = [];
    internal bool HasSavedPayload { get; init; }
}
