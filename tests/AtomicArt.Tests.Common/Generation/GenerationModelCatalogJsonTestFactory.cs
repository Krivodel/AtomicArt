using System.Text.Json;

using AtomicArt.Contracts.Generation;

namespace AtomicArt.Tests.Common.Generation;

public static class GenerationModelCatalogJsonTestFactory
{
    public const string DefaultModelId = "test-model";
    public const string DefaultDisplayName = "Test Model";
    public const string Provider = "google";
    public const string ProviderModelId = "provider-test-model";
    public const string PanelId = GenerationPanelIds.NanoBanana;
    public const string Resolution = "1k";
    public const long MaxTotalAttachmentBytes = 2048;
    public const int OutputImageTokens = 1120;

    public static GenerationModelCatalogTestSnapshot ExpectedModelSnapshot { get; } = new(
        DefaultModelId,
        Provider,
        ProviderModelId,
        PanelId,
        new GenerationModelTemperatureMetadataDto(0.1d, 2d, 1d, 0.1d),
        OutputImageTokens);

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static string CreateJson(
        string? modelId = DefaultModelId,
        string? displayName = DefaultDisplayName,
        IReadOnlyList<string>? aspectRatios = null)
    {
        string[] defaultAspectRatios = [GenerationAspectRatios.Auto, "1:1"];
        IReadOnlyList<string> effectiveAspectRatios = aspectRatios ?? defaultAspectRatios;
        string modelIdJson = JsonSerializer.Serialize(modelId, JsonOptions);
        string displayNameJson = JsonSerializer.Serialize(displayName, JsonOptions);
        string aspectRatiosJson = JsonSerializer.Serialize(effectiveAspectRatios, JsonOptions);

        return
            $$"""
            {
              "models": [
                {
                  "id": {{modelIdJson}},
                  "displayName": {{displayNameJson}},
                  "provider": "{{Provider}}",
                  "providerModelId": "{{ProviderModelId}}",
                  "panelId": "{{PanelId}}",
                  "contextWindowTokens": 1000,
                  "maxOutputTokens": 500,
                  "maxPromptLength": 100,
                  "aspectRatios": {{aspectRatiosJson}},
                  "resolutions": [ "{{Resolution}}" ],
                  "generationCounts": [ 1 ],
                  "temperature": { "minimum": 0.1, "maximum": 2.0, "default": 1.0, "step": 0.1 },
                  "attachments": {
                    "maxCount": 1,
                    "maxSingleFileBytes": 1024,
                    "maxTotalBytes": {{MaxTotalAttachmentBytes}},
                    "supportedContentTypes": [ "image/png" ]
                  },
                  "pricing": {
                    "currencyCode": "USD",
                    "inputTokenUsdPerMillion": 0.25,
                    "textOutputTokenUsdPerMillion": 1.50,
                    "imageOutputTokenUsdPerMillion": 30.00,
                    "inputImageTokens": 1120,
                    "outputImageTokensByResolution": {
                      "{{Resolution}}": {{OutputImageTokens}}
                    }
                  }
                }
              ]
            }
            """;
    }

    public static GenerationModelCatalogDto CreateCatalog(
        string? modelId = DefaultModelId,
        string? displayName = DefaultDisplayName,
        IReadOnlyList<string>? aspectRatios = null)
    {
        string json = CreateJson(modelId, displayName, aspectRatios);
        GenerationModelCatalogDto? catalog =
            JsonSerializer.Deserialize<GenerationModelCatalogDto>(json, JsonOptions);

        return catalog
            ?? throw new InvalidOperationException("Generation model catalog test JSON is missing.");
    }

    public static GenerationModelCatalogTestSnapshot CreateSnapshot(
        GenerationModelMetadataDto metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);

        return new GenerationModelCatalogTestSnapshot(
            metadata.Id,
            metadata.Provider,
            metadata.ProviderModelId,
            metadata.PanelId,
            metadata.Temperature,
            metadata.Pricing.OutputImageTokensByResolution[Resolution]);
    }
}
