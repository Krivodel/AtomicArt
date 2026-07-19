using AtomicArt.Desktop.Services.State;

namespace AtomicArt.Desktop.Services.Gallery.State;

public sealed class GalleryStateSection : StateSection<GalleryState>
{
    public const string KeyValue = "gallery";

    private const string SectionFileName = "gallery.json";
    private const int CurrentSchemaVersion = 1;

    public GalleryStateSection()
        : base(KeyValue, SectionFileName, CurrentSchemaVersion)
    {
    }

    protected override GalleryState NormalizePayload(GalleryState? state)
    {
        if (state?.Items is null)
        {
            return new GalleryState();
        }

        return new GalleryState
        {
            Items = state.Items
                .Where(GalleryItemStateMapper.IsValid)
                .Select(GalleryItemStateMapper.NormalizeForDeserialization)
                .ToList()
        };
    }
}
