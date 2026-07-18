using System.Text.Json;

namespace AtomicArt.Desktop.Services.State;

public abstract class StateSection<TPayload> : IStateSection
    where TPayload : class, new()
{
    public abstract string Key { get; }
    public abstract string FileName { get; }
    public abstract int SchemaVersion { get; }
    public Type PayloadType => typeof(TPayload);

    public object CreateDefaultPayload()
    {
        return new TPayload();
    }

    public object DeserializePayload(
        int schemaVersion,
        JsonElement payload,
        JsonSerializerOptions options)
    {
        TPayload? state = payload.Deserialize<TPayload>(options);

        return NormalizePayload(state);
    }

    protected abstract TPayload NormalizePayload(TPayload? state);
}
