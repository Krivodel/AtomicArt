using System.Text.Json;

using FluentAssertions;
using Xunit;

using AtomicArt.Contracts.Generation;

namespace AtomicArt.Contracts.Tests.Generation;

public sealed class GenerationModelCatalogContractsSerializationTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

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
        json.Should().Contain("\"pricing\"");
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
                    [GenerationAspectRatios.Auto],
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
                        1.50m,
                        30.00m,
                        1120,
                        new Dictionary<string, int>
                        {
                            ["1k"] = 1120
                        }),
                    new GenerationModelThinkingMetadataDto(
                        [
                            new("minimal", "Минимальный"),
                            new("high", "Максимальный")
                        ],
                        "minimal"))
        ]);
    }
}
