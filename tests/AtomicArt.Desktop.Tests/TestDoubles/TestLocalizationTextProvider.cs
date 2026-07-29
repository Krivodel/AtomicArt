using System.Globalization;

using AtomicArt.Desktop.Services.Localization;

namespace AtomicArt.Desktop.Tests.TestDoubles;

internal sealed class TestLocalizationTextProvider : ILocalizationTextProvider
{
    public static TestLocalizationTextProvider Default { get; } = new();

    private readonly IReadOnlyDictionary<string, string> _strings;

    public TestLocalizationTextProvider(
        IReadOnlyDictionary<string, string>? strings = null)
    {
        _strings = strings ?? BuiltInLocalizationCatalog.Current.English.Strings;
    }

    public string Get(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        return _strings.TryGetValue(key, out string? value)
            ? value
            : key;
    }

    public string Format(string key, params object?[] arguments)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(arguments);

        return string.Format(CultureInfo.CurrentCulture, Get(key), arguments);
    }
}
