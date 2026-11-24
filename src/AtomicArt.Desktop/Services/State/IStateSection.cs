using System.Text.Json;

namespace AtomicArt.Desktop.Services.State;

public interface IStateSection
{
    StateStorageArea StorageArea { get; }
    string Key { get; }
    string FileName { get; }
    int SchemaVersion { get; }
    Type PayloadType { get; }

    object CreateDefaultPayload();

    object DeserializePayload(
        int schemaVersion,
        JsonElement payload,
        JsonSerializerOptions options);
}
