using AtomicArt.Desktop.Services.State;

namespace AtomicArt.Desktop.Services.Settings;

public sealed class SettingsStateSection : StateSection<SettingsState>
{
    public const string KeyValue = "settings";
    public const string SectionFileName = "settings.json";

    private const int CurrentSchemaVersion = 1;

    public override string Key => KeyValue;
    public override string FileName => SectionFileName;
    public override int SchemaVersion => CurrentSchemaVersion;

    protected override SettingsState NormalizePayload(SettingsState? state)
    {
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
