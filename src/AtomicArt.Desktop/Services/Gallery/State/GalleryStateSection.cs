using System.Text.Json;

using AtomicArt.Desktop.Services.State;

namespace AtomicArt.Desktop.Services.Gallery.State;

public sealed class GalleryStateSection : IStateSection
{
    private const string SectionKey = "gallery";
    private const string SectionFileName = "gallery.json";
    private const int CurrentSchemaVersion = 1;

    public string Key => SectionKey;
    public string FileName => SectionFileName;
    public int SchemaVersion => CurrentSchemaVersion;
    public Type PayloadType => typeof(GalleryState);

    public object CreateDefaultPayload()
    {
        return new GalleryState();
    }

    public object DeserializePayload(
        int schemaVersion,
        JsonElement payload,
        JsonSerializerOptions options)
    {
        GalleryState? state = payload.Deserialize<GalleryState>(options);

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
