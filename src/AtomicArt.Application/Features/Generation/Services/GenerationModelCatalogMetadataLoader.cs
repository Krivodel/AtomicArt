using System.Text.Json;
using System.Text.Json.Nodes;

using AtomicArt.Contracts.Generation;
using AtomicArt.Domain.Exceptions;
using AtomicArt.Domain.Generation;

namespace AtomicArt.Application.Features.Generation.Services;

public static class GenerationModelCatalogMetadataLoader
{
    private const string ProfilesPropertyName = "profiles";
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public static GenerationModelCatalogDto LoadJson(string json, string sourceName)
    {
        ArgumentNullException.ThrowIfNull(json);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceName);

        if (string.IsNullOrWhiteSpace(json))
        {
            throw new InvalidOperationException(
                $"Model metadata source '{sourceName}' is empty.");
        }

        GenerationModelCatalogDto? catalog = DeserializeCatalog(json, sourceName);

        if (catalog is null)
        {
            throw new InvalidOperationException(
                $"Model metadata source '{sourceName}' does not contain a catalog.");
        }

        return CreateCatalogSnapshot(catalog, sourceName);
    }

    private static GenerationModelCatalogDto? DeserializeCatalog(string json, string sourceName)
    {
        try
        {
            JsonNode? parsedJson = JsonNode.Parse(json);

            if (parsedJson is null)
            {
                return null;
            }

            if (parsedJson is not JsonObject catalogObject)
            {
                return parsedJson.Deserialize<GenerationModelCatalogDto>(SerializerOptions);
            }

            JsonObject expandedCatalog = ExpandProfiles(catalogObject, sourceName);

            return expandedCatalog.Deserialize<GenerationModelCatalogDto>(SerializerOptions);
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException(
                $"Model metadata source '{sourceName}' contains malformed JSON.",
                exception);
        }
    }

    private static JsonObject ExpandProfiles(JsonObject catalog, string sourceName)
    {
        JsonObject expandedCatalog = catalog.DeepClone().AsObject();

        if (!expandedCatalog.TryGetPropertyValue(
                ProfilesPropertyName,
                out JsonNode? profilesNode))
        {
            return expandedCatalog;
        }

        if (profilesNode is not JsonObject profiles)
        {
            throw new InvalidOperationException(
                $"Model metadata source '{sourceName}' contains invalid profiles.");
        }

        expandedCatalog.Remove(ProfilesPropertyName);

        if (expandedCatalog["models"] is not JsonArray models)
        {
            return expandedCatalog;
        }

        for (int index = 0; index < models.Count; index++)
        {
            if (models[index] is not JsonObject model
                || !model.TryGetPropertyValue(
                    ProfilesPropertyName,
                    out JsonNode? modelProfilesNode))
            {
                continue;
            }

            if (modelProfilesNode is not JsonArray modelProfiles)
            {
                throw new InvalidOperationException(
                    $"Model metadata source '{sourceName}' contains invalid profiles for model at index {index}.");
            }

            JsonObject expandedModel = new();

            foreach (JsonNode? profileNameNode in modelProfiles)
            {
                if (profileNameNode is not JsonValue profileNameValue
                    || !profileNameValue.TryGetValue(out string? profileName)
                    || string.IsNullOrWhiteSpace(profileName)
                    || profiles[profileName] is not JsonObject profile)
                {
                    throw new InvalidOperationException(
                        $"Model metadata source '{sourceName}' contains an unknown profile for model at index {index}.");
                }

                MergeObject(expandedModel, profile);
            }

            JsonObject modelOverrides = model.DeepClone().AsObject();
            modelOverrides.Remove(ProfilesPropertyName);
            MergeObject(expandedModel, modelOverrides);
            models[index] = expandedModel;
        }

        return expandedCatalog;
    }

    private static void MergeObject(JsonObject target, JsonObject source)
    {
        foreach (KeyValuePair<string, JsonNode?> property in source)
        {
            if (property.Value is JsonObject sourceObject
                && target[property.Key] is JsonObject targetObject)
            {
                MergeObject(targetObject, sourceObject);

                continue;
            }

            target[property.Key] = property.Value?.DeepClone();
        }
    }

    private static GenerationModelCatalogDto CreateCatalogSnapshot(
        GenerationModelCatalogDto catalog,
        string sourceName)
    {
        if (catalog.Models is null || catalog.Models.Count == 0)
        {
            throw new InvalidOperationException(
                $"Model metadata source '{sourceName}' contains an empty catalog.");
        }

        Dictionary<string, int> modelIndexesById = new Dictionary<string, int>(StringComparer.Ordinal);
        List<GenerationModelMetadataDto> modelSnapshots = [];

        for (int index = 0; index < catalog.Models.Count; index++)
        {
            GenerationModelMetadataDto? metadata = catalog.Models[index];

            if (metadata is null)
            {
                throw new InvalidOperationException(
                    $"Model metadata source '{sourceName}' contains a null model entry at index {index}.");
            }

            GenerationModelMetadataDto modelSnapshot = CreateModelSnapshot(metadata, sourceName, index);

            if (!modelIndexesById.TryAdd(modelSnapshot.Id, index))
            {
                throw new InvalidOperationException(
                    $"Model metadata source '{sourceName}' contains duplicate model identifier '{modelSnapshot.Id}'.");
            }

            modelSnapshots.Add(modelSnapshot);
        }

        return new GenerationModelCatalogDto(modelSnapshots.AsReadOnly());
    }

    private static GenerationModelMetadataDto CreateModelSnapshot(
        GenerationModelMetadataDto metadata,
        string sourceName,
        int index)
    {
        string modelDescription = CreateModelDescription(metadata, index);
        string modelId = RequireTextForModelDescription(metadata.Id, "id", sourceName, modelDescription);
        string displayName = RequireTextForModelDescription(metadata.DisplayName, "displayName", sourceName, modelDescription);
        modelDescription = CreateModelDescription(modelId, displayName);
        string provider = RequireTextForModelDescription(metadata.Provider, "provider", sourceName, modelDescription);
        string providerModelId = RequireTextForModelDescription(metadata.ProviderModelId, "providerModelId", sourceName, modelDescription);
        string panelId = RequireTextForModelDescription(metadata.PanelId, "panelId", sourceName, modelDescription);
        int contextWindowTokens = RequirePositive(
            metadata.ContextWindowTokens,
            "contextWindowTokens",
            modelId,
            sourceName);
        int maxOutputTokens = RequirePositive(metadata.MaxOutputTokens, "maxOutputTokens", modelId, sourceName);

        if (metadata.Attachments is null)
        {
            throw new InvalidOperationException(
                $"Model metadata source '{sourceName}' contains model '{modelId}' without attachments.");
        }

        if (metadata.Temperature is null)
        {
            throw new InvalidOperationException(
                $"Model metadata source '{sourceName}' contains model '{modelId}' without temperature.");
        }

        GenerationModelConstraints constraints = CreateConstraints(metadata, sourceName);

        if (metadata.Pricing is null)
        {
            throw new InvalidOperationException(
                $"Model metadata source '{sourceName}' contains model '{modelId}' without pricing.");
        }

        GenerationModelPricing pricing = CreatePricing(modelId, metadata.Pricing, sourceName);
        GenerationModelThinkingMetadataDto? thinking = CreateThinkingSnapshot(
            metadata.Thinking,
            constraints.Thinking,
            modelId,
            sourceName);

        GenerationModelMetadataDto normalizedMetadata = metadata with
        {
            Id = modelId,
            DisplayName = displayName,
            Provider = provider,
            ProviderModelId = providerModelId,
            PanelId = panelId,
            ContextWindowTokens = contextWindowTokens,
            MaxOutputTokens = maxOutputTokens
        };

        return GenerationModelMetadataDomainMapper.ToMetadataSnapshot(
            normalizedMetadata,
            constraints,
            pricing,
            thinking);
    }

    private static GenerationModelThinkingMetadataDto? CreateThinkingSnapshot(
        GenerationModelThinkingMetadataDto? metadata,
        GenerationModelThinkingConstraints? constraints,
        string modelId,
        string sourceName)
    {
        if (metadata is null)
        {
            return null;
        }

        if (constraints is null || metadata.Levels is null)
        {
            throw new InvalidOperationException(
                $"Model metadata source '{sourceName}' contains model '{modelId}' with invalid thinking levels.");
        }

        List<GenerationModelThinkingLevelMetadataDto> levels = [];

        for (int index = 0; index < metadata.Levels.Count; index++)
        {
            GenerationModelThinkingLevelMetadataDto? level = metadata.Levels[index];

            if (level is null || string.IsNullOrWhiteSpace(level.DisplayName))
            {
                throw new InvalidOperationException(
                    $"Model metadata source '{sourceName}' contains model '{modelId}' without a display name for thinking level at index {index}.");
            }

            levels.Add(new GenerationModelThinkingLevelMetadataDto(
                constraints.Levels[index],
                level.DisplayName.Trim()));
        }

        return new GenerationModelThinkingMetadataDto(
            levels.AsReadOnly(),
            constraints.Default);
    }

    private static GenerationModelConstraints CreateConstraints(
        GenerationModelMetadataDto metadata,
        string sourceName)
    {
        try
        {
            return GenerationModelMetadataDomainMapper.ToConstraints(metadata);
        }
        catch (DomainException exception)
        {
            throw new InvalidOperationException(
                $"Model metadata source '{sourceName}' contains model '{metadata.Id}' with invalid constraints: {exception.Message}",
                exception);
        }
    }

    private static GenerationModelPricing CreatePricing(
        string modelId,
        GenerationModelPricingMetadataDto pricing,
        string sourceName)
    {
        try
        {
            return GenerationModelMetadataDomainMapper.ToPricing(modelId, pricing);
        }
        catch (DomainException exception)
        {
            throw new InvalidOperationException(
                $"Model metadata source '{sourceName}' contains model '{modelId}' with invalid pricing: {exception.Message}",
                exception);
        }
    }

    private static string RequireTextForModelDescription(
        string? value,
        string fieldName,
        string sourceName,
        string modelDescription)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException(
                $"Model metadata source '{sourceName}' contains {modelDescription} without required field {fieldName}.");
        }

        return value.Trim();
    }

    private static string CreateModelDescription(GenerationModelMetadataDto metadata, int index)
    {
        if (!string.IsNullOrWhiteSpace(metadata.Id) && !string.IsNullOrWhiteSpace(metadata.DisplayName))
        {
            return CreateModelDescription(metadata.Id.Trim(), metadata.DisplayName.Trim());
        }

        if (!string.IsNullOrWhiteSpace(metadata.Id))
        {
            return $"model '{metadata.Id.Trim()}'";
        }

        if (!string.IsNullOrWhiteSpace(metadata.DisplayName))
        {
            return $"model '{metadata.DisplayName.Trim()}'";
        }

        return $"model at index {index}";
    }

    private static string CreateModelDescription(string modelId, string displayName)
    {
        return $"model '{displayName}' with identifier '{modelId}'";
    }

    private static int RequirePositive(int value, string fieldName, string modelId, string sourceName)
    {
        if (value <= 0)
        {
            throw new InvalidOperationException(
                $"Model metadata source '{sourceName}' contains model '{modelId}' with non-positive {fieldName}.");
        }

        return value;
    }
}
