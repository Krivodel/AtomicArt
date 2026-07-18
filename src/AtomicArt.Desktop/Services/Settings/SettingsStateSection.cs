using System.Text.Json;

using AtomicArt.Desktop.Services.State;

namespace AtomicArt.Desktop.Services.Settings;

public sealed class SettingsStateSection : IStateSection
{
    public const string KeyValue = "settings";
    public const string SectionFileName = "settings.json";

    private const int CurrentSchemaVersion = 1;

    public string Key => KeyValue;
    public string FileName => SectionFileName;
    public int SchemaVersion => CurrentSchemaVersion;
    public Type PayloadType => typeof(SettingsState);

    public object CreateDefaultPayload()
    {
        return new SettingsState();
    }

    public object DeserializePayload(
        int schemaVersion,
        JsonElement payload,
        JsonSerializerOptions options)
    {
        SettingsState? state = payload.Deserialize<SettingsState>(options);

        if (state?.Values is null)
        {
            return new SettingsState();
        }

        Dictionary<string, string> values = state.Values
            .Where(pair => !string.IsNullOrWhiteSpace(pair.Key) && pair.Value is not null)
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);

        return new SettingsState
        {
            Values = values
        };
    }
}
