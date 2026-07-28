using System.Text.Json;
using System.Text.Json.Nodes;

namespace AtomicArt.Infrastructure.Generation;

public static class TestGenerationModelMetadataLoader
{
    private const string TestModelPropertyName = "testModel";

    private static readonly JsonSerializerOptions SerializerOptions =
        new(JsonSerializerDefaults.Web);

    public static TestGenerationModelMetadata? LoadOptionalJson(
        string json,
        string sourceName)
    {
        ArgumentNullException.ThrowIfNull(json);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceName);

        try
        {
            JsonNode? root = JsonNode.Parse(json);

            if (root is not JsonObject document
                || document[TestModelPropertyName] is not JsonNode metadataNode)
            {
                return null;
            }

            TestGenerationModelMetadata? metadata =
                metadataNode.Deserialize<TestGenerationModelMetadata>(
                    SerializerOptions);

            return metadata is null
                ? throw CreateInvalidMetadataException(sourceName)
                : CreateSnapshot(metadata, sourceName);
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException(
                $"Model metadata source '{sourceName}' contains malformed test model metadata.",
                exception);
        }
    }

    private static TestGenerationModelMetadata CreateSnapshot(
        TestGenerationModelMetadata metadata,
        string sourceName)
    {
        string[] aspectRatios = CreateRequiredValuesSnapshot(
            metadata.AspectRatios,
            nameof(metadata.AspectRatios),
            sourceName);
        string[] resolutions = CreateRequiredValuesSnapshot(
            metadata.Resolutions,
            nameof(metadata.Resolutions),
            sourceName);

        return new TestGenerationModelMetadata
        {
            AspectRatios = aspectRatios,
            DisplayName = GetRequiredText(
                metadata.DisplayName,
                nameof(metadata.DisplayName),
                sourceName),
            Id = GetRequiredText(
                metadata.Id,
                nameof(metadata.Id),
                sourceName),
            ProviderModelId = GetRequiredText(
                metadata.ProviderModelId,
                nameof(metadata.ProviderModelId),
                sourceName),
            Resolutions = resolutions
        };
    }

    private static string[] CreateRequiredValuesSnapshot(
        IReadOnlyList<string>? values,
        string propertyName,
        string sourceName)
    {
        if (values is null || values.Count == 0)
        {
            throw CreateInvalidMetadataException(sourceName);
        }

        string[] snapshot = values
            .Select(value => GetRequiredText(value, propertyName, sourceName))
            .ToArray();

        return snapshot.Distinct(StringComparer.Ordinal).Count()
            == snapshot.Length
            ? snapshot
            : throw CreateInvalidMetadataException(sourceName);
    }

    private static string GetRequiredText(
        string? value,
        string propertyName,
        string sourceName)
    {
        return string.IsNullOrWhiteSpace(value)
            ? throw new InvalidOperationException(
                $"Model metadata source '{sourceName}' contains test model metadata without required property '{propertyName}'.")
            : value.Trim();
    }

    private static InvalidOperationException CreateInvalidMetadataException(
        string sourceName)
    {
        return new InvalidOperationException(
            $"Model metadata source '{sourceName}' contains invalid test model metadata.");
    }
}
