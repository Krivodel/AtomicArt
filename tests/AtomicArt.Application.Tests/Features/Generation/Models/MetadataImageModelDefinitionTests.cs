using FluentAssertions;
using Xunit;

using AtomicArt.Application.Common.Models;
using AtomicArt.Application.Features.Generation.Commands.CreateImageGeneration;
using AtomicArt.Application.Features.Generation.Models;
using AtomicArt.Application.Tests.Generation;
using AtomicArt.Contracts.Generation;
using AtomicArt.Domain.Generation;
using AtomicArt.Tests.Common.Generation;

namespace AtomicArt.Application.Tests.Features.Generation.Models;

public sealed class MetadataImageModelDefinitionTests
{
    private const string GifContentType = "image/gif";
    private const string PngContentType = "image/png";
    private static readonly byte[] GifBytes = GenerationImageFileSignatures.Gif89A.ToArray();
    private static readonly byte[] PngBytes = [.. GenerationImageFileSignatures.Png, 0x00];

    [Fact]
    public void Metadata_WithNanoBanana2_ReturnsDisplayName()
    {
        MetadataImageModelDefinition definition = MetadataImageModelTestFactory.CreateDefinition();

        string displayName = definition.Metadata.DisplayName;

        displayName.Should().Be(ApiModelMetadataTestCatalog.NanoBanana2DisplayName);
    }

    [Fact]
    public void Constraints_WithNanoBanana2_ReturnsCatalogContentTypes()
    {
        MetadataImageModelDefinition definition = MetadataImageModelTestFactory.CreateDefinition();
        GenerationModelMetadataDto metadata = ApiModelMetadataTestCatalog.LoadNanoBanana2Metadata();

        IReadOnlyList<string> contentTypes = definition.Constraints.SupportedContentTypes;

        contentTypes.Should().Equal(metadata.Attachments.SupportedContentTypes);
    }

    [Fact]
    public void Constraints_WithNanoBanana2_ReturnsAggregateAttachmentLimit()
    {
        MetadataImageModelDefinition definition = MetadataImageModelTestFactory.CreateDefinition();

        long maxTotalAttachedImageBytes = definition.Constraints.MaxTotalAttachedImageBytes;

        maxTotalAttachedImageBytes.Should().BeGreaterThan(definition.Constraints.MaxAttachedImageBytes);
    }

    [Fact]
    public void Validate_WithUnsupportedResolution_ReturnsError()
    {
        MetadataImageModelDefinition definition = MetadataImageModelTestFactory.CreateDefinition();
        ImageGenerationRequestDto request = CreateRequest(resolution: "1x1");

        Result<ImageGenerationRequestDto> result = definition.Validate(request);

        result.IsValidationError.Should().BeTrue();
        result.ErrorCode.Should().Be(GenerationErrorCodes.UnsupportedResolution);
    }

    [Fact]
    public void Validate_WithAutoAspectRatio_ReturnsSuccess()
    {
        MetadataImageModelDefinition definition = MetadataImageModelTestFactory.CreateDefinition();
        ImageGenerationRequestDto request = CreateRequest(aspectRatio: GenerationAspectRatios.Auto);

        Result<ImageGenerationRequestDto> result = definition.Validate(request);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Validate_WithUnsupportedTemperature_ReturnsError()
    {
        MetadataImageModelDefinition definition = MetadataImageModelTestFactory.CreateDefinition();
        ImageGenerationRequestDto request = CreateRequest(temperature: 0.15d);

        Result<ImageGenerationRequestDto> result = definition.Validate(request);

        result.IsValidationError.Should().BeTrue();
        result.ErrorCode.Should().Be(GenerationErrorCodes.ModelRequestValidation);
    }

    [Fact]
    public void Validate_WithoutThinkingLevel_AppliesMetadataDefault()
    {
        MetadataImageModelDefinition definition = MetadataImageModelTestFactory.CreateDefinition();
        ImageGenerationRequestDto request = CreateRequest();

        Result<ImageGenerationRequestDto> result = definition.Validate(request);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value?.ThinkingLevel.Should().Be("low");
    }

    [Fact]
    public void Validate_WithUnsupportedThinkingLevel_ReturnsError()
    {
        MetadataImageModelDefinition definition = MetadataImageModelTestFactory.CreateDefinition();
        ImageGenerationRequestDto request = CreateRequest(thinkingLevel: "medium");

        Result<ImageGenerationRequestDto> result = definition.Validate(request);

        result.IsValidationError.Should().BeTrue();
        result.ErrorCode.Should().Be(GenerationErrorCodes.ModelRequestValidation);
    }

    [Fact]
    public void Validate_WithThinkingLevelForNanoBananaPro_ReturnsError()
    {
        GenerationModelMetadataDto metadata = ApiModelMetadataTestCatalog.LoadNanoBananaProMetadata();
        MetadataImageModelDefinition definition = MetadataImageModelTestFactory.CreateDefinition(metadata);
        ImageGenerationRequestDto request = CreateRequest(metadata, thinkingLevel: "high");

        Result<ImageGenerationRequestDto> result = definition.Validate(request);

        result.IsValidationError.Should().BeTrue();
        result.ErrorCode.Should().Be(GenerationErrorCodes.ModelRequestValidation);
    }

    [Fact]
    public void Validate_WithTooManyAttachments_ReturnsError()
    {
        MetadataImageModelDefinition definition = MetadataImageModelTestFactory.CreateDefinition();
        IReadOnlyList<AttachedImageDto> attachedImages = Enumerable
            .Range(0, definition.Constraints.MaxAttachedImages + 1)
            .Select(index => CreateAttachedImage($"image-{index}.png"))
            .ToList();
        ImageGenerationRequestDto request = CreateRequest(attachedImages: attachedImages);

        Result<ImageGenerationRequestDto> result = definition.Validate(request);

        result.IsValidationError.Should().BeTrue();
        result.ErrorCode.Should().Be(GenerationErrorCodes.ModelRequestValidation);
    }

    [Fact]
    public void Validate_WithPromptLongerThanModelLimit_ReturnsError()
    {
        MetadataImageModelDefinition definition = MetadataImageModelTestFactory.CreateDefinition();
        string prompt = new('a', definition.Constraints.MaxPromptLength + 1);
        ImageGenerationRequestDto request = CreateRequest(prompt: prompt);

        Result<ImageGenerationRequestDto> result = definition.Validate(request);

        result.IsValidationError.Should().BeTrue();
        result.ErrorCode.Should().Be(GenerationErrorCodes.ModelRequestValidation);
    }

    [Fact]
    public void Validate_WithInvalidImageSignature_ReturnsError()
    {
        MetadataImageModelDefinition definition = MetadataImageModelTestFactory.CreateDefinition();
        AttachedImageDto attachedImage = new("image.png", PngContentType, [0x00, 0x01, 0x02]);
        IReadOnlyList<AttachedImageDto> attachedImages = new List<AttachedImageDto> { attachedImage };
        ImageGenerationRequestDto request = CreateRequest(attachedImages: attachedImages);

        Result<ImageGenerationRequestDto> result = definition.Validate(request);

        result.IsValidationError.Should().BeTrue();
        result.ErrorCode.Should().Be(GenerationErrorCodes.ModelRequestValidation);
    }

    [Fact]
    public void Validate_WithUnsupportedModelAttachmentContentType_ReturnsError()
    {
        MetadataImageModelDefinition definition = MetadataImageModelTestFactory.CreateDefinition();
        AttachedImageDto attachedImage = new("image.gif", GifContentType, GifBytes);
        IReadOnlyList<AttachedImageDto> attachedImages = new List<AttachedImageDto> { attachedImage };
        ImageGenerationRequestDto request = CreateRequest(attachedImages: attachedImages);

        Result<ImageGenerationRequestDto> result = definition.Validate(request);

        result.IsValidationError.Should().BeTrue();
        result.ErrorCode.Should().Be(GenerationErrorCodes.ModelRequestValidation);
    }

    private static ImageGenerationRequestDto CreateRequest(
        string prompt = "Prompt",
        string? aspectRatio = null,
        string? resolution = null,
        double? temperature = null,
        IReadOnlyList<AttachedImageDto>? attachedImages = null,
        string? thinkingLevel = null)
    {
        GenerationModelMetadataDto metadata = ApiModelMetadataTestCatalog.LoadNanoBanana2Metadata();

        return CreateRequest(
            metadata,
            prompt,
            aspectRatio,
            resolution,
            temperature,
            attachedImages,
            thinkingLevel);
    }

    private static ImageGenerationRequestDto CreateRequest(
        GenerationModelMetadataDto metadata,
        string prompt = "Prompt",
        string? aspectRatio = null,
        string? resolution = null,
        double? temperature = null,
        IReadOnlyList<AttachedImageDto>? attachedImages = null,
        string? thinkingLevel = null)
    {

        return ImageGenerationRequestDtoTestFactory.Create(
            modelId: metadata.Id,
            prompt: prompt,
            aspectRatio: aspectRatio ?? metadata.AspectRatios.First(),
            resolution: resolution ?? metadata.Resolutions.First(),
            temperature: temperature ?? metadata.Temperature.Default,
            attachedImages: attachedImages,
            thinkingLevel: thinkingLevel);
    }

    private static AttachedImageDto CreateAttachedImage(string fileName)
    {
        return new AttachedImageDto(fileName, PngContentType, PngBytes);
    }
}
