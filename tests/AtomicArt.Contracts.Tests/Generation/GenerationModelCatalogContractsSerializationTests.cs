using System.Text.Json;
using System.Text.Json.Nodes;

using FluentAssertions;
using Xunit;

using AtomicArt.Contracts.Generation;

namespace AtomicArt.Contracts.Tests.Generation;

public sealed class GenerationModelCatalogContractsSerializationTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public void Deserialize_WithoutTemperatureCapability_PreservesLegacySupport()
    {
        GenerationModelMetadataDto metadata = CreateCatalog().Models.Single();
        JsonObject json = JsonSerializer.SerializeToNode(metadata, JsonOptions)?.AsObject()
            ?? throw new InvalidOperationException("Model metadata JSON is required.");
        json.Remove("supportsTemperature");

        GenerationModelMetadataDto? deserialized = json.Deserialize<GenerationModelMetadataDto>(JsonOptions);

        deserialized.Should().NotBeNull();
        deserialized?.SupportsTemperature.Should().BeTrue();
    }

    [Fact]
    public void SerializeAndDeserialize_WithTemperatureUnsupported_PreservesCapability()
    {
        GenerationModelMetadataDto metadata = CreateCatalog().Models.Single() with
        {
            SupportsTemperature = false
        };

        string json = JsonSerializer.Serialize(metadata, JsonOptions);
        GenerationModelMetadataDto? deserialized = JsonSerializer.Deserialize<GenerationModelMetadataDto>(
            json,
            JsonOptions);

        deserialized.Should().BeEquivalentTo(metadata);
    }

    [Fact]
    public void SerializeAndDeserialize_WithPricingMetadata_PreservesContractShape()
    {
        GenerationModelCatalogDto catalog = CreateCatalog();

        string json = JsonSerializer.Serialize(catalog, JsonOptions);
        GenerationModelCatalogDto? deserialized = JsonSerializer.Deserialize<GenerationModelCatalogDto>(
            json,
            JsonOptions);

        json.Should().Contain("\"models\"");
        json.Should().Contain("\"displayName\"");
        json.Should().Contain("\"providerModelId\"");
        json.Should().Contain("\"panelId\"");
        json.Should().Contain("\"temperature\"");
        json.Should().Contain("\"default\":1");
        json.Should().Contain("\"thinking\"");
        json.Should().Contain("\"value\":\"high\"");
        json.Should().Contain(
            $"\"localizationKey\":\"{GenerationLocalizationKeys.ThinkingHigh}\"");
        json.Should().Contain("\"pricing\"");
        json.Should().Contain("\"cachedInputTokenPriceMultiplier\":0.1");
        json.Should().Contain("\"outputImageTokensByResolution\"");
        json.Should().Contain("\"attachments\"");
        json.Should().Contain("\"supportedContentTypes\"");
        deserialized.Should().BeEquivalentTo(catalog);
    }

    private static GenerationModelCatalogDto CreateCatalog()
    {
        return new GenerationModelCatalogDto(
        [
            new(
                    "test-model",
                    "Test Model",
                    "google",
                    "provider-test-model",
                    GenerationPanelIds.NanoBanana,
                    1000,
                    500,
                    100,
                    [
                        new GenerationModelOptionMetadataDto(
                            GenerationAspectRatios.Auto,
                            GenerationLocalizationKeys.OptionsAuto)
                    ],
                    ["1k"],
                    [1],
                    new GenerationModelTemperatureMetadataDto(0.1d, 2d, 1d, 0.1d),
                    new GenerationModelAttachmentMetadataDto(
                        1,
                        1024,
                        2048,
                        ["image/png"]),
                    new GenerationModelPricingMetadataDto(
                        "USD",
                        0.25m,
                        0.1m,
                        1.50m,
                        30.00m,
                        4m,
                        1120,
                        new Dictionary<string, int>
                        {
                            ["1k"] = 1120
                        }),
                    new GenerationModelThinkingMetadataDto(
                        [
                            new("minimal", GenerationLocalizationKeys.ThinkingLow),
                            new("high", GenerationLocalizationKeys.ThinkingHigh)
                        ],
                        "minimal"))
        ]);
    }
}
