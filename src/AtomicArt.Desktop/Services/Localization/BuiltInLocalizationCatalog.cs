using System.Reflection;
using System.Text.Json;

namespace AtomicArt.Desktop.Services.Localization;

internal sealed class BuiltInLocalizationCatalog
{
    internal static BuiltInLocalizationCatalog Current => Catalog.Value;

    internal LocalizationSnapshot English { get; }
    internal LocalizationSnapshot Russian { get; }
    internal JsonElement EnglishStrings { get; }

    private static readonly Lazy<BuiltInLocalizationCatalog> Catalog =
        new(Load, LazyThreadSafetyMode.ExecutionAndPublication);

    private BuiltInLocalizationCatalog(
        LocalizationSnapshot english,
        LocalizationSnapshot russian,
        JsonElement englishStrings)
    {
        English = english ?? throw new ArgumentNullException(nameof(english));
        Russian = russian ?? throw new ArgumentNullException(nameof(russian));
        EnglishStrings = englishStrings;
    }

    private static BuiltInLocalizationCatalog Load()
    {
        (LocalizationSnapshot english, JsonElement englishStrings) =
            LoadSnapshot(LocalizationConstants.EnglishId, sortOrder: 1);
        (LocalizationSnapshot russian, _) =
            LoadSnapshot(LocalizationConstants.RussianId, sortOrder: 0);
        ValidateMatchingKeys(english, russian);

        return new BuiltInLocalizationCatalog(
            english,
            russian,
            englishStrings);
    }

    private static (LocalizationSnapshot Snapshot, JsonElement Strings)
        LoadSnapshot(
            string localizationId,
            int sortOrder)
    {
        Assembly assembly = typeof(BuiltInLocalizationCatalog).Assembly;
        string resourceSuffix = string.Concat(
            ".Resources.Localizations.",
            localizationId,
            LocalizationConstants.JsonExtension);
        string? resourceName = assembly
            .GetManifestResourceNames()
            .SingleOrDefault(name =>
                name.EndsWith(resourceSuffix, StringComparison.Ordinal));

        if (resourceName is null)
        {
            throw new InvalidDataException(
                $"Embedded localization resource '{localizationId}' is missing.");
        }

        using Stream stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidDataException(
                $"Embedded localization resource '{resourceName}' could not be opened.");
        LocalizationDocument document = LocalizationDocumentParser.Parse(
            stream,
            resourceName);
        LocalizationSnapshot snapshot = new(
            localizationId,
            document.Culture,
            document.Strings,
            IsBuiltIn: true,
            sortOrder);

        return (snapshot, document.StringsElement);
    }

    private static void ValidateMatchingKeys(
        LocalizationSnapshot english,
        LocalizationSnapshot russian)
    {
        IReadOnlyList<string> missingRussianKeys = english.Strings.Keys
            .Except(russian.Strings.Keys, StringComparer.Ordinal)
            .ToList();
        IReadOnlyList<string> extraRussianKeys = russian.Strings.Keys
            .Except(english.Strings.Keys, StringComparer.Ordinal)
            .ToList();

        if (missingRussianKeys.Count > 0 || extraRussianKeys.Count > 0)
        {
            throw new InvalidDataException(
                "Built-in English and Russian localizations must contain identical key sets.");
        }
    }
}
