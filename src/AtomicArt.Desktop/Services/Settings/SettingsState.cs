namespace AtomicArt.Desktop.Services.Settings;

public sealed class SettingsState
{
    public Dictionary<string, string> Values { get; init; } = [];

    public static SettingsState FromValues(IEnumerable<KeyValuePair<string, string>> values)
    {
        ArgumentNullException.ThrowIfNull(values);

        return new SettingsState
        {
            Values = values.ToDictionary(
                pair => pair.Key,
                pair => pair.Value,
                StringComparer.Ordinal)
        };
    }
}
