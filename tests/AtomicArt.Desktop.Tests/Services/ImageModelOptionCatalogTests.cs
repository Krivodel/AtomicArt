using System.Text.Json;

using FluentAssertions;
using Xunit;

using AtomicArt.Contracts.Generation;
using AtomicArt.Desktop.Services;

namespace AtomicArt.Desktop.Tests.Services;

public sealed class ImageModelOptionCatalogTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("test\u0001model")]
    public void Initialize_WithInvalidModelId_ThrowsInvalidOperationException(string? modelId)
    {
        ImageModelOptionCatalog catalog = new();
        GenerationModelCatalogDto dto = CreateCatalog(modelId: modelId);

        Action act = () => catalog.Initialize(dto);

        act.Should().Throw<InvalidOperationException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Test\u0001Model")]
    public void Initialize_WithInvalidDisplayName_ThrowsInvalidOperationException(string? displayName)
    {
        ImageModelOptionCatalog catalog = new();
        GenerationModelCatalogDto dto = CreateCatalog(displayName: displayName);

        Action act = () => catalog.Initialize(dto);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Initialize_WithPricingMetadata_PreservesPricingMetadata()
    {
        ImageModelOptionCatalog catalog = new();
        GenerationModelCatalogDto dto = CreateCatalog();

        catalog.Initialize(dto);

        ImageModelOption option = catalog.GetModels().Single();
        option.Provider.Should().Be("google");
        option.ProviderModelId.Should().Be("provider-test-model");
        option.PanelId.Should().Be(GenerationPanelIds.NanoBanana);
        option.ContextWindowTokens.Should().Be(1000);
        option.MaxOutputTokens.Should().Be(500);
        option.Temperature.Should().Be(new GenerationModelTemperatureMetadataDto(0.1d, 2d, 1d, 0.1d));
        option.Pricing.CurrencyCode.Should().Be("USD");
        option.Pricing.OutputImageTokensByResolution["1k"].Should().Be(1120);
    }

    private static GenerationModelCatalogDto CreateCatalog(
        string? modelId = "test-model",
        string? displayName = "Test Model")
    {
        string modelIdJson = modelId is null ? "null" : JsonSerializer.Serialize(modelId, JsonOptions);
        string displayNameJson = displayName is null ? "null" : JsonSerializer.Serialize(displayName, JsonOptions);
        string json =
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
                  "aspectRatios": [ "авто" ],
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

        GenerationModelCatalogDto? catalog = JsonSerializer.Deserialize<GenerationModelCatalogDto>(
            json,
            JsonOptions);

        return catalog ?? throw new InvalidOperationException("Catalog must be deserialized.");
    }
}
