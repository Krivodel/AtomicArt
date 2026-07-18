using FluentAssertions;
using Xunit;

using AtomicArt.Contracts.Generation;
using AtomicArt.Domain.Generation;
using CommonApiModelMetadataTestCatalog =
    AtomicArt.Tests.Common.Generation.ApiModelMetadataTestCatalog;

namespace AtomicArt.Domain.Tests.Generation;

public sealed class GenerationModelRulesTests
{
    [Fact]
    public void Validate_WithSupportedNanoBanana2Parameters_ReturnsValid()
    {
        GenerationModelRules rules = CreateRules();
        GenerationModelConstraints constraints = CreateConstraints();
        IReadOnlyList<GenerationAttachedImage> attachedImages = CreateAttachedImages(1_024L);
        GenerationValidationRequest request = CreateRequest(
            constraints,
            attachedImages: attachedImages);

        GenerationValidationResult result = rules.Validate(request);

        result.IsValid.Should().BeTrue();
        result.ErrorCode.Should().BeNull();
        result.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void Validate_WithUnsupportedResolution_ReturnsResolutionError()
    {
        GenerationModelRules rules = CreateRules();
        GenerationModelConstraints constraints = CreateConstraints();
        GenerationValidationRequest request = CreateRequest(
            constraints,
            resolution: "1x1");

        GenerationValidationResult result = rules.Validate(request);

        result.IsValid.Should().BeFalse();
        result.ErrorCode.Should().Be(GenerationErrorCodes.UnsupportedResolution);
    }

    [Fact]
    public void Validate_WithUnsupportedAspectRatio_ReturnsAspectRatioError()
    {
        GenerationModelRules rules = CreateRules();
        GenerationModelConstraints constraints = CreateConstraints();
        GenerationValidationRequest request = CreateRequest(
            constraints,
            aspectRatio: "2:1");

        GenerationValidationResult result = rules.Validate(request);

        result.IsValid.Should().BeFalse();
        result.ErrorCode.Should().Be(GenerationErrorCodes.UnsupportedAspectRatio);
    }

    [Fact]
    public void Validate_WithUnsupportedGenerationCount_ReturnsModelRequestError()
    {
        GenerationModelRules rules = CreateRules();
        GenerationModelConstraints constraints = CreateConstraints();
        GenerationValidationRequest request = CreateRequest(
            constraints,
            generationCount: 5);

        GenerationValidationResult result = rules.Validate(request);

        result.IsValid.Should().BeFalse();
        result.ErrorCode.Should().Be(GenerationErrorCodes.ModelRequestValidation);
    }

    [Fact]
    public void Validate_WithUnsupportedTemperature_ReturnsModelRequestError()
    {
        GenerationModelRules rules = CreateRules();
        GenerationModelConstraints constraints = CreateConstraints();
        GenerationValidationRequest request = CreateRequest(
            constraints,
            temperature: 0.15d);

        GenerationValidationResult result = rules.Validate(request);

        result.IsValid.Should().BeFalse();
        result.ErrorCode.Should().Be(GenerationErrorCodes.ModelRequestValidation);
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
        GenerationValidationRequest request = CreateRequest(
            constraints,
            attachedImages: attachedImages);

        GenerationValidationResult result = rules.Validate(request);

        result.IsValid.Should().BeFalse();
        result.ErrorCode.Should().Be(GenerationErrorCodes.ModelRequestValidation);
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
        GenerationValidationRequest request = CreateRequest(
            constraints,
            attachedImages: attachedImages);

        GenerationValidationResult result = rules.Validate(request);

        result.IsValid.Should().BeFalse();
        result.ErrorCode.Should().Be(GenerationErrorCodes.ModelRequestValidation);
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
        GenerationValidationRequest request = CreateRequest(
            constraints,
            attachedImages: attachedImages);

        GenerationValidationResult result = rules.Validate(request);

        result.IsValid.Should().BeFalse();
        result.ErrorCode.Should().Be(GenerationErrorCodes.ModelRequestValidation);
    }

    [Fact]
    public void Validate_WithMultipleMatchingRules_UsesHighestPriorityRule()
    {
        GenerationValidationResult expectedResult = GenerationValidationResult.Invalid(
            "ERR-TEST-001",
            "Selected highest-priority rule.");
        IGenerationModelRules[] modelRules =
        [
            new TestGenerationModelRules(0, _ => true, GenerationValidationResult.Valid()),
            new TestGenerationModelRules(10, _ => true, expectedResult)
        ];
        GenerationModelRules rules = new(modelRules);
        GenerationModelConstraints constraints = CreateConstraints();

        GenerationValidationResult result = Validate(rules, constraints);

        result.Should().Be(expectedResult);
    }

    [Fact]
    public void Validate_WithoutMatchingRules_ThrowsInvalidOperationException()
    {
        IGenerationModelRules[] modelRules =
        [
            new TestGenerationModelRules(0, _ => false, GenerationValidationResult.Valid())
        ];
        GenerationModelRules rules = new(modelRules);
        GenerationModelConstraints constraints = CreateConstraints();

        Action action = () => Validate(rules, constraints);

        action.Should().Throw<InvalidOperationException>()
            .WithMessage($"No rules are registered for generation model '{constraints.ModelId}'.");
    }

    [Fact]
    public void Validate_WithMatchingRulesAtSameHighestPriority_ThrowsInvalidOperationException()
    {
        IGenerationModelRules[] modelRules =
        [
            new TestGenerationModelRules(10, _ => true, GenerationValidationResult.Valid()),
            new TestGenerationModelRules(10, _ => true, GenerationValidationResult.Valid())
        ];
        GenerationModelRules rules = new(modelRules);
        GenerationModelConstraints constraints = CreateConstraints();

        Action action = () => Validate(rules, constraints);

        action.Should().Throw<InvalidOperationException>()
            .WithMessage(
                $"Multiple rules with priority 10 are registered for generation model '{constraints.ModelId}'.");
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
        GenerationModelMetadataDto metadata =
            CommonApiModelMetadataTestCatalog.LoadNanoBanana2Metadata();

        return new GenerationModelConstraints(
            modelId ?? metadata.Id,
            metadata.MaxPromptLength,
            metadata.AspectRatios,
            metadata.Resolutions,
            metadata.GenerationCounts,
            new GenerationModelTemperatureConstraints(
                metadata.Temperature.Minimum,
                metadata.Temperature.Maximum,
                metadata.Temperature.Default,
                metadata.Temperature.Step),
            metadata.Attachments.MaxCount,
            metadata.Attachments.MaxSingleFileBytes,
            metadata.Attachments.MaxTotalBytes,
            metadata.Attachments.SupportedContentTypes);
    }

    private static GenerationValidationResult Validate(
        GenerationModelRules rules,
        GenerationModelConstraints constraints)
    {
        return rules.Validate(CreateRequest(constraints));
    }

    private static GenerationValidationRequest CreateRequest(
        GenerationModelConstraints constraints,
        string? prompt = "Prompt",
        string? aspectRatio = null,
        string? resolution = null,
        double? temperature = null,
        int generationCount = 1,
        IReadOnlyList<GenerationAttachedImage>? attachedImages = null,
        string? thinkingLevel = null)
    {
        return new GenerationValidationRequest(
            constraints,
            prompt,
            aspectRatio ?? constraints.AspectRatios.First(),
            resolution ?? constraints.Resolutions.First(),
            temperature ?? constraints.Temperature.Default,
            generationCount,
            attachedImages ?? Array.Empty<GenerationAttachedImage>(),
            thinkingLevel);
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

    private sealed class TestGenerationModelRules : IGenerationModelRules
    {
        public int Priority { get; }

        private readonly Func<GenerationModelConstraints, bool> _canValidate;
        private readonly GenerationValidationResult _result;

        public TestGenerationModelRules(
            int priority,
            Func<GenerationModelConstraints, bool> canValidate,
            GenerationValidationResult result)
        {
            Priority = priority;
            _canValidate = canValidate ?? throw new ArgumentNullException(nameof(canValidate));
            _result = result ?? throw new ArgumentNullException(nameof(result));
        }

        public bool CanValidate(GenerationModelConstraints constraints)
        {
            ArgumentNullException.ThrowIfNull(constraints);

            return _canValidate(constraints);
        }

        public GenerationValidationResult Validate(GenerationValidationRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);

            return _result;
        }
    }
}
