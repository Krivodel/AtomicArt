using FluentAssertions;
using Moq;
using Xunit;

using AtomicArt.Application.Features.Generation.Interfaces;
using AtomicArt.Application.Features.Generation.Queries.GetGenerationModels;
using AtomicArt.Contracts.Generation;
using AtomicArt.Tests.Common.Generation;

namespace AtomicArt.Application.Tests.Features.Generation.Queries.GetGenerationModels;

public sealed class GetGenerationModelsHandlerTests
{
    [Fact]
    public async Task Handle_WithRegistryModels_ReturnsCatalog()
    {
        IReadOnlyList<GenerationModelMetadataDto> models =
            GenerationModelCatalogJsonTestFactory.CreateCatalog().Models;
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
