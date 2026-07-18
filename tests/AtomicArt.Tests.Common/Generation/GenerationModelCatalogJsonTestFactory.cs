using System.Text.Json;

using AtomicArt.Contracts.Generation;

namespace AtomicArt.Tests.Common.Generation;

public static class GenerationModelCatalogJsonTestFactory
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static string CreateJson(
        string? modelId = "test-model",
        string? displayName = "Test Model",
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
                  "provider": "google",
                  "providerModelId": "provider-test-model",
                  "panelId": "nano-banana",
                  "contextWindowTokens": 1000,
                  "maxOutputTokens": 500,
                  "maxPromptLength": 100,
                  "aspectRatios": {{aspectRatiosJson}},
                  "resolutions": [ "1k" ],
                  "generationCounts": [ 1 ],
                  "temperature": { "minimum": 0.1, "maximum": 2.0, "default": 1.0, "step": 0.1 },
                  "attachments": {
                    "maxCount": 1,
                    "maxSingleFileBytes": 1024,
                    "maxTotalBytes": 2048,
                    "supportedContentTypes": [ "image/png" ]
                  },
                  "pricing": {
                    "currencyCode": "USD",
                    "inputTokenUsdPerMillion": 0.25,
                    "textOutputTokenUsdPerMillion": 1.50,
                    "imageOutputTokenUsdPerMillion": 30.00,
                    "inputImageTokens": 1120,
                    "outputImageTokensByResolution": {
                      "1k": 1120
                    }
                  }
                }
              ]
            }
            """;
    }

    public static GenerationModelCatalogDto CreateCatalog(
        string? modelId = "test-model",
        string? displayName = "Test Model",
        IReadOnlyList<string>? aspectRatios = null)
    {
        string json = CreateJson(modelId, displayName, aspectRatios);
        GenerationModelCatalogDto? catalog =
            JsonSerializer.Deserialize<GenerationModelCatalogDto>(json, JsonOptions);

        return catalog
            ?? throw new InvalidOperationException("Generation model catalog test JSON is missing.");
    }
}
