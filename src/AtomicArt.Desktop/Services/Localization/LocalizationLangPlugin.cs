using System.Globalization;
using System.Reflection;

using Lang.Avalonia;

namespace AtomicArt.Desktop.Services.Localization;

internal sealed class LocalizationLangPlugin : ILangPlugin
{
    public CultureInfo Culture { get; set; }

    private readonly LocalizationTextResolver _textResolver;

    internal LocalizationLangPlugin(LocalizationTextResolver textResolver)
    {
        _textResolver = textResolver
            ?? throw new ArgumentNullException(nameof(textResolver));
        Culture = textResolver.Active.Culture;
    }

    public void Load(CultureInfo cultureInfo)
    {
        Culture = cultureInfo ?? throw new ArgumentNullException(nameof(cultureInfo));
    }

    public void AddResource(params Assembly[] assemblies)
    {
        ArgumentNullException.ThrowIfNull(assemblies);
    }

    public List<LocalizationLanguage>? GetLanguages()
    {
        List<LocalizationLanguage> languages =
        [
            CreateLanguage(_textResolver.Active)
        ];

        if (!string.Equals(
                _textResolver.Active.Id,
                _textResolver.English.Id,
                StringComparison.OrdinalIgnoreCase))
        {
            languages.Add(CreateLanguage(_textResolver.English));
        }

        return languages;
    }

    public string GetResource(string key, string? cultureName = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        return _textResolver.Get(key, cultureName);
    }

    private static LocalizationLanguage CreateLanguage(LocalizationSnapshot snapshot)
    {
        LocalizationLanguage language = new()
        {
            Language = snapshot.Id,
            Description = snapshot.Id,
            CultureName = snapshot.Culture.Name
        };

        foreach (KeyValuePair<string, string> item in snapshot.Strings)
        {
            language.Languages[item.Key] = item.Value;
        }

        return language;
    }
}
