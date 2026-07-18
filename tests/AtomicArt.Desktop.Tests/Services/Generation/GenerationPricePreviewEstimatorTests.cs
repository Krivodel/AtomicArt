using FluentAssertions;
using Xunit;

using AtomicArt.Contracts.Generation;
using AtomicArt.Desktop.Services;
using AtomicArt.Desktop.Services.Generation;
using AtomicArt.Desktop.Tests.Generation;

namespace AtomicArt.Desktop.Tests.Services.Generation;

public sealed class GenerationPricePreviewEstimatorTests
{
    private readonly GenerationPricePreviewEstimator _estimator = new();

    [Fact]
    public void Estimate_WithPromptAttachmentResolutionAndGenerationCount_ReturnsEstimatedPrice()
    {
        GenerationModelMetadataDto metadata = ApiModelMetadataTestCatalog.LoadNanoBanana2Metadata();
        NanoBanana2GenerationParameters parameters = CreateParameters(
            metadata,
            prompt: "abcd",
            resolution: "512",
            generationCount: 2,
            attachedImages:
            [
                new("reference.png", "image/png", [0x01])
            ]);

        GenerationPriceDto? price = _estimator.Estimate(parameters);

        price.Should().BeEquivalentTo(new GenerationPriceDto(
            0.0902005m,
            metadata.Pricing.CurrencyCode,
            GenerationPricePreviewEstimator.Source));
    }

    [Fact]
    public void Estimate_WithUnsupportedResolution_ReturnsNull()
    {
        GenerationModelMetadataDto metadata = ApiModelMetadataTestCatalog.LoadNanoBanana2Metadata();
        NanoBanana2GenerationParameters parameters = CreateParameters(
            metadata,
            prompt: "Prompt",
            resolution: "unsupported",
            generationCount: 1,
            attachedImages: []);

        GenerationPriceDto? price = _estimator.Estimate(parameters);

        price.Should().BeNull();
    }

    private static NanoBanana2GenerationParameters CreateParameters(
        GenerationModelMetadataDto metadata,
        string prompt,
        string resolution,
        int generationCount,
        IReadOnlyList<AttachedImageDto> attachedImages)
    {
        ImageModelOption selectedModel = new(
            metadata.Id,
            metadata.DisplayName,
            metadata.Provider,
            metadata.ProviderModelId,
            metadata.PanelId,
            metadata.ContextWindowTokens,
            metadata.MaxOutputTokens,
            metadata.AspectRatios,
            metadata.Resolutions,
            metadata.GenerationCounts,
            metadata.Temperature,
            metadata.Attachments.MaxCount,
            checked((int)metadata.Attachments.MaxSingleFileBytes),
            metadata.Attachments.MaxTotalBytes,
            metadata.Attachments.SupportedContentTypes,
            metadata.Pricing);

        return new NanoBanana2GenerationParameters(
            selectedModel,
            metadata.DisplayName,
            prompt,
            metadata.AspectRatios.First(),
            resolution,
            metadata.Temperature.Default,
            generationCount,
            attachedImages);
    }
}
