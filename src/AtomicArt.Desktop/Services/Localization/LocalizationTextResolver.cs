namespace AtomicArt.Desktop.Services.Localization;

internal sealed class LocalizationTextResolver
{
    public LocalizationSnapshot Active { get; }
    public LocalizationSnapshot English { get; }

    internal LocalizationTextResolver(
        LocalizationSnapshot active,
        LocalizationSnapshot english)
    {
        Active = active ?? throw new ArgumentNullException(nameof(active));
        English = english ?? throw new ArgumentNullException(nameof(english));
    }

    internal string Get(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        return Active.Strings.TryGetValue(key, out string? activeValue)
            ? activeValue
            : GetEnglishOrKey(key);
    }

    internal string Get(string key, string? cultureName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        if (!string.IsNullOrWhiteSpace(cultureName)
            && string.Equals(
                cultureName,
                English.Culture.Name,
                StringComparison.OrdinalIgnoreCase))
        {
            return GetEnglishOrKey(key);
        }

        return Get(key);
    }

    private string GetEnglishOrKey(string key)
    {
        return English.Strings.TryGetValue(key, out string? englishValue)
            ? englishValue
            : key;
    }
}
