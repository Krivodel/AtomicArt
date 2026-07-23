using FluentAssertions;
using Xunit;

using AtomicArt.Application.Features.Generation.Models;
using AtomicArt.Application.Tests.Generation;
using AtomicArt.Contracts.Generation;
using AtomicArt.Tests.Common.Generation;

namespace AtomicArt.Application.Tests.Features.Generation.Models;

public sealed class MetadataImageModelDefinitionTests
{
    [Fact]
    public void Metadata_WithNanoBanana2_ReturnsDisplayName()
    {
        GenerationModelMetadataDto metadata =
            ApiModelMetadataTestCatalog.LoadNanoBanana2Metadata();
        MetadataImageModelDefinition definition =
            MetadataImageModelTestFactory.CreateDefinition(metadata);

        definition.Metadata.DisplayName.Should().Be(metadata.DisplayName);
    }

    [Fact]
    public void Constraints_WithNanoBanana2_ReturnsCatalogContentTypes()
    {
        GenerationModelMetadataDto metadata =
            ApiModelMetadataTestCatalog.LoadNanoBanana2Metadata();
        MetadataImageModelDefinition definition =
            MetadataImageModelTestFactory.CreateDefinition(metadata);

        definition.Constraints.SupportedContentTypes.Should()
            .BeEquivalentTo(metadata.Attachments.SupportedContentTypes);
    }

    [Fact]
    public void Constraints_WithNanoBanana2_ReturnsAggregateAttachmentLimit()
    {
        GenerationModelMetadataDto metadata =
            ApiModelMetadataTestCatalog.LoadNanoBanana2Metadata();
        MetadataImageModelDefinition definition =
            MetadataImageModelTestFactory.CreateDefinition(metadata);

        definition.Constraints.MaxTotalAttachedImageBytes.Should()
            .Be(metadata.Attachments.MaxTotalBytes);
    }
}
