using FluentAssertions;
using Xunit;

using AtomicArt.Domain.Generation;

namespace AtomicArt.Domain.Tests.Generation;

public sealed class GenerationModelRulesTests
{
    [Fact]
    public void Validate_WithSupportedNanoBanana2Parameters_ReturnsValid()
    {
        GenerationModelRules rules = CreateRules();
        GenerationModelConstraints constraints = CreateConstraints();
        IReadOnlyList<GenerationAttachedImage> attachedImages = CreateAttachedImages(1_024L);

        GenerationValidationResult result = rules.Validate(
            constraints,
            "Prompt",
            constraints.AspectRatios.First(),
            constraints.Resolutions.First(),
            constraints.Temperature.Default,
            1,
            attachedImages);

        result.IsValid.Should().BeTrue();
        result.ErrorCode.Should().BeNull();
        result.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void Validate_WithUnsupportedResolution_ReturnsResolutionError()
    {
        GenerationModelRules rules = CreateRules();
        GenerationModelConstraints constraints = CreateConstraints();
        IReadOnlyList<GenerationAttachedImage> attachedImages = [];

        GenerationValidationResult result = rules.Validate(
            constraints,
            "Prompt",
            constraints.AspectRatios.First(),
            "1x1",
            constraints.Temperature.Default,
            1,
            attachedImages);

        result.IsValid.Should().BeFalse();
        result.ErrorCode.Should().Be("ERR-GEN-002");
    }

    [Fact]
    public void Validate_WithUnsupportedAspectRatio_ReturnsAspectRatioError()
    {
        GenerationModelRules rules = CreateRules();
        GenerationModelConstraints constraints = CreateConstraints();
        IReadOnlyList<GenerationAttachedImage> attachedImages = [];

        GenerationValidationResult result = rules.Validate(
            constraints,
            "Prompt",
            "2:1",
            constraints.Resolutions.First(),
            constraints.Temperature.Default,
            1,
            attachedImages);

        result.IsValid.Should().BeFalse();
        result.ErrorCode.Should().Be("ERR-GEN-003");
    }

    [Fact]
    public void Validate_WithUnsupportedGenerationCount_ReturnsModelRequestError()
    {
        GenerationModelRules rules = CreateRules();
        GenerationModelConstraints constraints = CreateConstraints();
        IReadOnlyList<GenerationAttachedImage> attachedImages = [];

        GenerationValidationResult result = rules.Validate(
            constraints,
            "Prompt",
            constraints.AspectRatios.First(),
            constraints.Resolutions.First(),
            constraints.Temperature.Default,
            5,
            attachedImages);

        result.IsValid.Should().BeFalse();
        result.ErrorCode.Should().Be("ERR-GEN-004");
    }

    [Fact]
    public void Validate_WithUnsupportedTemperature_ReturnsModelRequestError()
    {
        GenerationModelRules rules = CreateRules();
        GenerationModelConstraints constraints = CreateConstraints();

        GenerationValidationResult result = rules.Validate(
            constraints,
            "Prompt",
            constraints.AspectRatios.First(),
            constraints.Resolutions.First(),
            0.15d,
            1,
            []);

        result.IsValid.Should().BeFalse();
        result.ErrorCode.Should().Be("ERR-GEN-004");
    }

    [Fact]
    public void Validate_WithTooManyAttachments_ReturnsModelRequestError()
    {
        GenerationModelRules rules = CreateRules();
        GenerationModelConstraints constraints = CreateConstraints();
        IReadOnlyList<GenerationAttachedImage> attachedImages = Enumerable
            .Range(0, constraints.MaxAttachedImages + 1)
            .Select(_ => CreateAttachedImage(1_024L))
            .ToList();

        GenerationValidationResult result = rules.Validate(
            constraints,
            "Prompt",
            constraints.AspectRatios.First(),
            constraints.Resolutions.First(),
            constraints.Temperature.Default,
            1,
            attachedImages);

        result.IsValid.Should().BeFalse();
        result.ErrorCode.Should().Be("ERR-GEN-004");
    }

    [Fact]
    public void Validate_WithTotalAttachmentSizeLimitExceeded_ReturnsModelRequestError()
    {
        GenerationModelRules rules = CreateRules();
        GenerationModelConstraints constraints = CreateConstraints();
        IReadOnlyList<GenerationAttachedImage> attachedImages =
        [
            CreateAttachedImage(constraints.MaxTotalAttachedImageBytes),
            CreateAttachedImage(1L)
        ];

        GenerationValidationResult result = rules.Validate(
            constraints,
            "Prompt",
            constraints.AspectRatios.First(),
            constraints.Resolutions.First(),
            constraints.Temperature.Default,
            1,
            attachedImages);

        result.IsValid.Should().BeFalse();
        result.ErrorCode.Should().Be("ERR-GEN-004");
    }

    [Fact]
    public void Validate_WithUnsupportedAttachmentContentType_ReturnsModelRequestError()
    {
        GenerationModelRules rules = CreateRules();
        GenerationModelConstraints constraints = CreateConstraints();
        IReadOnlyList<GenerationAttachedImage> attachedImages =
        [
            new("image/gif", 1_024L)
        ];

        GenerationValidationResult result = rules.Validate(
            constraints,
            "Prompt",
            constraints.AspectRatios.First(),
            constraints.Resolutions.First(),
            constraints.Temperature.Default,
            1,
            attachedImages);

        result.IsValid.Should().BeFalse();
        result.ErrorCode.Should().Be("ERR-GEN-004");
    }

    [Fact]
    public void Resolutions_WithConstraints_ReturnsReadOnlyValues()
    {
        GenerationModelConstraints constraints = CreateConstraints();

        IReadOnlyList<string> resolutions = constraints.Resolutions;

        resolutions.Should().NotBeAssignableTo<string[]>();
        resolutions.Should().NotBeEmpty();
    }

    private static GenerationModelRules CreateRules()
    {
        IGenerationModelRules[] modelRules =
        [
            new MetadataGenerationModelRules()
        ];

        return new GenerationModelRules(modelRules);
    }

    private static GenerationModelConstraints CreateConstraints(string? modelId = null)
    {
        return ApiModelMetadataTestCatalog.LoadNanoBanana2Constraints(modelId);
    }

    private static IReadOnlyList<GenerationAttachedImage> CreateAttachedImages(long sizeInBytes)
    {
        return
        [
            CreateAttachedImage(sizeInBytes)
        ];
    }

    private static GenerationAttachedImage CreateAttachedImage(long sizeInBytes)
    {
        return new GenerationAttachedImage("image/png", sizeInBytes);
    }
}
