using FluentAssertions;
using Xunit;

using AtomicArt.Contracts.Generation;
using AtomicArt.Infrastructure.Generation;
using AtomicArt.Tests.Common.Generation;

namespace AtomicArt.Infrastructure.Tests.Generation;

public sealed class TestGenerationModelCatalogAugmenterTests
{
    [Fact]
    public void AddTestModelIfEnabled_WithNanoBananaMetadata_InheritsFamilyConstraints()
    {
        GenerationModelMetadataDto baseMetadata = ApiModelMetadataTestCatalog.LoadNanoBanana2Metadata();
        List<GenerationModelMetadataDto> models = [baseMetadata];
        GenerationModelCatalogDto catalog = new(models);
        TestGenerationOptions options = new()
        {
            Enabled = true
        };

        GenerationModelCatalogDto augmentedCatalog =
            TestGenerationModelCatalogAugmenter.AddTestModelIfEnabled(catalog, options);

        GenerationModelMetadataDto testMetadata = augmentedCatalog.Models
            .Single(model => model.Id == TestGenerationModelCatalogAugmenter.ModelId);
        testMetadata.PanelId.Should().Be(baseMetadata.PanelId);
        testMetadata.ContextWindowTokens.Should().Be(baseMetadata.ContextWindowTokens);
        testMetadata.MaxOutputTokens.Should().Be(baseMetadata.MaxOutputTokens);
        testMetadata.MaxPromptLength.Should().Be(baseMetadata.MaxPromptLength);
        testMetadata.GenerationCounts.Should().BeSameAs(baseMetadata.GenerationCounts);
        testMetadata.Temperature.Should().BeSameAs(baseMetadata.Temperature);
        testMetadata.Attachments.MaxCount.Should().Be(baseMetadata.Attachments.MaxCount);
        testMetadata.Attachments.MaxSingleFileBytes.Should()
            .Be(baseMetadata.Attachments.MaxSingleFileBytes);
        testMetadata.Attachments.MaxTotalBytes.Should()
            .Be(baseMetadata.Attachments.MaxTotalBytes);
        testMetadata.Thinking.Should().BeNull();
        testMetadata.TransportLimits.Should().NotBeNull();
        testMetadata.TransportLimits?.AllowedResponseContentTypes.Should()
            .BeEquivalentTo(
                GenerationImageFileFormats.All.Select(
                    format => format.ContentType));
        long encodedMaximumImageBytes =
            ((options.MaxImageBytes + 2L) / 3L) * 4L;
        testMetadata.TransportLimits?.MaxResponseBytes.Should()
            .BeGreaterThan(encodedMaximumImageBytes);
    }
}
