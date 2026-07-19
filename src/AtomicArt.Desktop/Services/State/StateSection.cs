using System.Text.Json;

namespace AtomicArt.Desktop.Services.State;

public abstract class StateSection<TPayload> : IStateSection
    where TPayload : class, new()
{
    public string Key => _key;
    public string FileName => _fileName;
    public int SchemaVersion => _schemaVersion;
    public Type PayloadType => typeof(TPayload);

    private readonly string _key;
    private readonly string _fileName;
    private readonly int _schemaVersion;

    protected StateSection(string key, string fileName, int schemaVersion)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(schemaVersion);

        _key = key;
        _fileName = fileName;
        _schemaVersion = schemaVersion;
    }

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
