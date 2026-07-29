using System.Globalization;
using System.Text.Json;

namespace AtomicArt.Desktop.Services.Localization;

internal static class LocalizationDocumentParser
{
    private const int MaximumJsonDepth = 64;

    internal static LocalizationDocument Parse(
        Stream stream,
        string sourceName)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceName);

        using JsonDocument document = JsonDocument.Parse(
            stream,
            CreateDocumentOptions());

        return ParseDocument(document, sourceName);
    }

    internal static async Task<LocalizationDocument> ParseAsync(
        Stream stream,
        string sourceName,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceName);

        using JsonDocument document = await JsonDocument
            .ParseAsync(stream, CreateDocumentOptions(), ct)
            .ConfigureAwait(false);

        return ParseDocument(document, sourceName);
    }

    private static JsonDocumentOptions CreateDocumentOptions()
    {
        return new JsonDocumentOptions
        {
            AllowTrailingCommas = false,
            CommentHandling = JsonCommentHandling.Disallow,
            MaxDepth = MaximumJsonDepth
        };
    }

    private static LocalizationDocument ParseDocument(
        JsonDocument document,
        string sourceName)
    {
        JsonElement root = document.RootElement;

        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException(
                $"Localization source '{sourceName}' must contain a JSON object.");
        }

        int schemaVersion = ReadSchemaVersion(root, sourceName);

        if (schemaVersion != LocalizationConstants.SchemaVersion)
        {
            throw new InvalidDataException(
                $"Localization source '{sourceName}' uses unsupported schema version '{schemaVersion}'.");
        }

        CultureInfo culture = ReadCulture(root, sourceName);
        JsonElement stringsElement = ReadStringsElement(root, sourceName);
        Dictionary<string, string> strings = new(StringComparer.Ordinal);
        CollectStrings(stringsElement, string.Empty, strings, sourceName);

        return new LocalizationDocument(
            culture,
            strings,
            stringsElement.Clone());
    }

    private static int ReadSchemaVersion(JsonElement root, string sourceName)
    {
        if (!root.TryGetProperty("schemaVersion", out JsonElement schemaVersionElement)
            || schemaVersionElement.ValueKind != JsonValueKind.Number
            || !schemaVersionElement.TryGetInt32(out int schemaVersion))
        {
            throw new InvalidDataException(
                $"Localization source '{sourceName}' must contain an integer schemaVersion.");
        }

        return schemaVersion;
    }

    private static CultureInfo ReadCulture(JsonElement root, string sourceName)
    {
        if (!root.TryGetProperty("culture", out JsonElement cultureElement)
            || cultureElement.ValueKind != JsonValueKind.String
            || string.IsNullOrWhiteSpace(cultureElement.GetString()))
        {
            throw new InvalidDataException(
                $"Localization source '{sourceName}' must contain a non-empty culture.");
        }

        string cultureName = cultureElement.GetString() ?? string.Empty;

        try
        {
            return CultureInfo.GetCultureInfo(cultureName);
        }
        catch (CultureNotFoundException exception)
        {
            throw new InvalidDataException(
                $"Localization source '{sourceName}' contains invalid culture '{cultureName}'.",
                exception);
        }
    }

    private static JsonElement ReadStringsElement(JsonElement root, string sourceName)
    {
        if (!root.TryGetProperty("strings", out JsonElement stringsElement)
            || stringsElement.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException(
                $"Localization source '{sourceName}' must contain a strings object.");
        }

        return stringsElement;
    }

    private static void CollectStrings(
        JsonElement element,
        string parentPath,
        IDictionary<string, string> strings,
        string sourceName)
    {
        foreach (JsonProperty property in element.EnumerateObject())
        {
            if (string.IsNullOrWhiteSpace(property.Name)
                || property.Name.Contains(".", StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    $"Localization source '{sourceName}' contains invalid string key segment '{property.Name}'.");
            }

            string key = string.IsNullOrEmpty(parentPath)
                ? property.Name
                : string.Concat(parentPath, ".", property.Name);

            if (property.Value.ValueKind == JsonValueKind.Object)
            {
                CollectStrings(property.Value, key, strings, sourceName);
                continue;
            }

            if (property.Value.ValueKind != JsonValueKind.String)
            {
                throw new InvalidDataException(
                    $"Localization source '{sourceName}' contains non-string value for key '{key}'.");
            }

            string value = property.Value.GetString() ?? string.Empty;

            if (!strings.TryAdd(key, value))
            {
                throw new InvalidDataException(
                    $"Localization source '{sourceName}' contains duplicate key '{key}'.");
            }
        }
    }
}
