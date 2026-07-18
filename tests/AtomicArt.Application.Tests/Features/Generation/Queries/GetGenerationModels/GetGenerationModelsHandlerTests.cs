using FluentAssertions;
using Moq;
using Xunit;

using AtomicArt.Application.Features.Generation.Interfaces;
using AtomicArt.Application.Features.Generation.Queries.GetGenerationModels;
using AtomicArt.Contracts.Generation;

namespace AtomicArt.Application.Tests.Features.Generation.Queries.GetGenerationModels;

public sealed class GetGenerationModelsHandlerTests
{
    [Fact]
    public async Task Handle_WithRegistryModels_ReturnsCatalog()
    {
        IReadOnlyList<GenerationModelMetadataDto> models =
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
                ["Auto"],
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
                    }))
        ];
        Mock<IImageModelRegistry> registry = new();
        registry
            .Setup(currentRegistry => currentRegistry.GetModels())
            .Returns(models);
        GetGenerationModelsHandler handler = new(registry.Object);
        GetGenerationModelsQuery query = new();

        GenerationModelCatalogDto catalog = await handler.Handle(query, CancellationToken.None);

        catalog.Models.Should().ContainSingle();
        GenerationModelMetadataDto metadata = catalog.Models.Single();
        metadata.Id.Should().Be("test-model");
        metadata.Provider.Should().Be("google");
        metadata.ProviderModelId.Should().Be("provider-test-model");
        metadata.PanelId.Should().Be(GenerationPanelIds.NanoBanana);
        metadata.Temperature.Should().Be(new GenerationModelTemperatureMetadataDto(0.1d, 2d, 1d, 0.1d));
        metadata.Attachments.MaxTotalBytes.Should().Be(2048);
        metadata.Pricing.OutputImageTokensByResolution["1k"].Should().Be(1120);
        registry.Verify(currentRegistry => currentRegistry.GetModels(), Times.Once);
    }
}
