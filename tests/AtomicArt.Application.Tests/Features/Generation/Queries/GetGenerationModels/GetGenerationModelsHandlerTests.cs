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
        GenerationModelCatalogTestSnapshot snapshot =
            GenerationModelCatalogJsonTestFactory.CreateSnapshot(metadata);
        snapshot.Should().Be(GenerationModelCatalogJsonTestFactory.ExpectedModelSnapshot);
        metadata.Attachments.MaxTotalBytes.Should().Be(
            GenerationModelCatalogJsonTestFactory.MaxTotalAttachmentBytes);
        registry.Verify(currentRegistry => currentRegistry.GetModels(), Times.Once);
    }
}
