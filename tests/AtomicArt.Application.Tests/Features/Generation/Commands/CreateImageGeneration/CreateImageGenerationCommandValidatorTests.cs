using System.Text.Json;

using FluentAssertions;
using FluentValidation.Results;
using Moq;
using Xunit;

using AtomicArt.Application.Features.Generation.Commands.CreateImageGeneration;
using AtomicArt.Application.Features.Generation.Interfaces;
using AtomicArt.Application.Tests.Generation;
using AtomicArt.Contracts.Generation;
using AtomicArt.Tests.Common.Generation;
using TestGenerationCredentials = AtomicArt.Tests.Common.Generation.TestGenerationCredentials;

namespace AtomicArt.Application.Tests.Features.Generation.Commands.CreateImageGeneration;

public sealed class CreateImageGenerationCommandValidatorTests
{
    private const string GifContentType = "image/gif";
    private const string LocalModelId = "local-model";
    private const string PngContentType = "image/png";
    private const string WebpContentType = "image/webp";

    private static string ModelId => ApiModelMetadataTestCatalog.NanoBanana2ModelId;

    private static readonly byte[] PngBytes = GenerationImageTestData.PngSignatureBytes;
    private static readonly byte[] GifBytes = GenerationImageFileSignatures.Gif89A.ToArray();
    private static readonly byte[] WebpBytes =
    [
        .. GenerationImageFileSignatures.Riff,
        0x00,
        0x00,
        0x00,
        0x00,
        .. GenerationImageFileSignatures.Webp
    ];

    private readonly CreateImageGenerationCommandValidator _validator = CreateValidator();

    [Fact]
    public void Validate_WithEmptyModelId_HasValidationError()
    {
        CreateImageGenerationCommand command = CreateCommand(modelId: string.Empty);

        ValidationResult result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error => error.PropertyName == "Request.ModelId");
    }

    [Fact]
    public void Validate_WithBlankPrompt_HasValidationError()
    {
        CreateImageGenerationCommand command = CreateCommand(prompt: "   ");

        ValidationResult result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error => error.PropertyName == "Request.Prompt");
    }

    [Fact]
    public void Validate_WithNullAspectRatio_HasValidationError()
    {
        CreateImageGenerationCommand command = CreateCommandFromJson(
            $$"""
            {
              "modelId": "{{ModelId}}",
              "prompt": "Prompt",
              "aspectRatio": null,
              "resolution": "1k",
              "generationCount": 1,
              "attachedImages": []
            }
            """);

        ValidationResult result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error => error.PropertyName == "Request.AspectRatio");
    }

    [Fact]
    public void Validate_WithNullResolution_HasValidationError()
    {
        CreateImageGenerationCommand command = CreateCommandFromJson(
            $$"""
            {
              "modelId": "{{ModelId}}",
              "prompt": "Prompt",
              "aspectRatio": "Авто",
              "resolution": null,
              "generationCount": 1,
              "attachedImages": []
            }
            """);

        ValidationResult result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error => error.PropertyName == "Request.Resolution");
    }

    [Fact]
    public void Validate_WithZeroGenerationCount_HasValidationError()
    {
        CreateImageGenerationCommand command = CreateCommand(generationCount: 0);

        ValidationResult result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error => error.PropertyName == "Request.GenerationCount");
    }

    [Fact]
    public void Validate_WithNonFiniteTemperature_HasValidationError()
    {
        CreateImageGenerationCommand command = CreateCommand(temperature: double.NaN);

        ValidationResult result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error => error.PropertyName == "Request.Temperature");
    }

    [Fact]
    public void Validate_WithUnsupportedModelGenerationCount_IsValid()
    {
        CreateImageGenerationCommand command = CreateCommand(generationCount: 5);

        ValidationResult result = _validator.Validate(command);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WithNullAttachedImages_IsValid()
    {
        CreateImageGenerationCommand command = CreateCommandFromJson(
            $$"""
            {
              "modelId": "{{ModelId}}",
              "prompt": "Prompt",
              "aspectRatio": "РђРІС‚Рѕ",
              "resolution": "1024x1024",
              "generationCount": 1,
              "attachedImages": null
            }
            """);

        ValidationResult result = _validator.Validate(command);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WithNullAttachedImage_IsValid()
    {
        CreateImageGenerationCommand command = CreateCommand(
            attachedImages: new List<AttachedImageDto>
            {
                null!
            });

        ValidationResult result = _validator.Validate(command);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WithTooManyModelAttachedImages_IsValid()
    {
        IReadOnlyList<AttachedImageDto> attachedImages = Enumerable
            .Range(0, 11)
            .Select(index => new AttachedImageDto(
                $"reference-{index}.png",
                PngContentType,
                PngBytes))
            .ToList();
        CreateImageGenerationCommand command = CreateCommand(attachedImages: attachedImages);

        ValidationResult result = _validator.Validate(command);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WithLargeAttachedImageContent_IsValid()
    {
        byte[] content = GenerationImageTestData.CreatePngContent(PngBytes.Length + 1_024);
        CreateImageGenerationCommand command = CreateCommand(
            attachedImages:
            [
                new AttachedImageDto(
                    "reference.png",
                    PngContentType,
                    content)
            ]);

        ValidationResult result = _validator.Validate(command);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WithUnsupportedImageContentType_IsValid()
    {
        CreateImageGenerationCommand command = CreateCommand(
            attachedImages:
            [
                new AttachedImageDto(
                    "reference.txt",
                    "text/plain",
                    PngBytes)
            ]);

        ValidationResult result = _validator.Validate(command);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WithInvalidImageSignature_IsValid()
    {
        CreateImageGenerationCommand command = CreateCommand(
            attachedImages:
            [
                new AttachedImageDto(
                    "reference.png",
                    PngContentType,
                    [0x01, 0x02, 0x03])
            ]);

        ValidationResult result = _validator.Validate(command);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WithValidAttachedImage_IsValid()
    {
        CreateImageGenerationCommand command = CreateCommand(
            attachedImages:
            [
                new AttachedImageDto(
                    "reference.png",
                    PngContentType,
                    PngBytes)
            ]);

        ValidationResult result = _validator.Validate(command);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WithValidWebpAttachedImage_IsValid()
    {
        CreateImageGenerationCommand command = CreateCommand(
            attachedImages:
            [
                new AttachedImageDto(
                    "reference.webp",
                    WebpContentType,
                    WebpBytes)
            ]);

        ValidationResult result = _validator.Validate(command);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WithValidGifAttachedImage_IsValid()
    {
        CreateImageGenerationCommand command = CreateCommand(
            attachedImages:
            [
                new AttachedImageDto(
                    "reference.gif",
                    GifContentType,
                    GifBytes)
            ]);

        ValidationResult result = _validator.Validate(command);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WithEmptyAttachedImages_IsValid()
    {
        CreateImageGenerationCommand command = new(
            ImageGenerationRequestDtoTestFactory.Create(
                modelId: ModelId,
                aspectRatio: "Авто",
                resolution: "1k"),
            TestGenerationCredentials.ProviderCredential);

        ValidationResult result = _validator.Validate(command);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WithGoogleModelMissingProviderCredential_HasValidationError()
    {
        CreateImageGenerationCommand command = CreateCommand(providerCredential: null);

        ValidationResult result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error => error.PropertyName == "ProviderCredential");
    }

    [Fact]
    public void Validate_WithNonGoogleModelMissingProviderCredential_IsValid()
    {
        CreateImageGenerationCommand command = CreateCommand(
            modelId: LocalModelId,
            providerCredential: null);

        ValidationResult result = _validator.Validate(command);

        result.IsValid.Should().BeTrue();
    }

    private static CreateImageGenerationCommand CreateCommand(
        string? modelId = null,
        string prompt = "Prompt",
        double temperature = 1d,
        int generationCount = 1,
        IReadOnlyList<AttachedImageDto>? attachedImages = null,
        string? providerCredential = TestGenerationCredentials.ProviderCredential)
    {
        ImageGenerationRequestDto request = ImageGenerationRequestDtoTestFactory.Create(
            modelId: modelId ?? ModelId,
            prompt: prompt,
            aspectRatio: "Авто",
            resolution: "1k",
            temperature: temperature,
            generationCount: generationCount,
            attachedImages: attachedImages);

        return new CreateImageGenerationCommand(request, providerCredential);
    }

    private static CreateImageGenerationCommandValidator CreateValidator()
    {
        Mock<IImageModelRegistry> registry = new();
        registry
            .Setup(currentRegistry => currentRegistry.GetById(ModelId))
            .Returns(CreateModelDefinition(GenerationProviderIds.Google));
        registry
            .Setup(currentRegistry => currentRegistry.GetById(LocalModelId))
            .Returns(CreateModelDefinition("local"));

        return new CreateImageGenerationCommandValidator(registry.Object);
    }

    private static IImageModelDefinition CreateModelDefinition(string provider)
    {
        GenerationModelMetadataDto metadata = ApiModelMetadataTestCatalog
            .LoadNanoBanana2Metadata() with
        {
            Provider = provider
        };
        Mock<IImageModelDefinition> modelDefinition = new();
        modelDefinition
            .SetupGet(currentModelDefinition => currentModelDefinition.Metadata)
            .Returns(metadata);

        return modelDefinition.Object;
    }

    private static CreateImageGenerationCommand CreateCommandFromJson(string json)
    {
        ImageGenerationRequestDto? request = JsonSerializer.Deserialize<ImageGenerationRequestDto>(
            json,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        if (request is null)
        {
            throw new InvalidOperationException("Failed to deserialize test command.");
        }

        return new CreateImageGenerationCommand(
            request,
            TestGenerationCredentials.ProviderCredential);
    }
}
