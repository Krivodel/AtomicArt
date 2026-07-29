using System.Globalization;

namespace AtomicArt.Desktop.Services.Localization;

internal sealed record LocalizationSnapshot(
    string Id,
    CultureInfo Culture,
    IReadOnlyDictionary<string, string> Strings,
    bool IsBuiltIn,
    int SortOrder)
{
    internal LocalizationOption ToOption()
    {
        return new LocalizationOption(Id, Culture, IsBuiltIn);
    }
}
