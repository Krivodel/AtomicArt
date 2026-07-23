using System.Text.Json;

using FluentAssertions;
using Xunit;

using AtomicArt.Application.Common.Models;
using AtomicArt.Application.Features.Generation.Interfaces;
using AtomicArt.Application.Features.Generation.Models;
using AtomicArt.Application.Features.Generation.Services;
using AtomicArt.Contracts.Generation;
using AtomicArt.Domain.Generation;
using AtomicArt.Tests.Common.Generation;

namespace AtomicArt.Application.Tests.Features.Generation.Services;

public sealed class StreamingGenerationRequestValidatorTests
{
    private static readonly Guid LogicalGenerationId =
        Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    public async Task ValidateAsync_WithUnknownParameter_ReturnsValidationError()
    {
        TestContext context = CreateContext();
        Dictionary<string, JsonElement> parameters =
            CreateValidParameters();
        parameters["unknownParameter"] =
            JsonSerializer.SerializeToElement(true);
        GenerationRequestMetadataDto metadata = CreateMetadata(parameters);

        Result<StreamingImageGenerationRequest> result =
            await context.Validator.ValidateAsync(
                metadata,
                Array.Empty<IGenerationAttachmentSource>(),
                context.ModelDefinition,
                CancellationToken.None);

        result.IsValidationError.Should().BeTrue();
        result.ErrorCode.Should().Be(
            GenerationProtocolErrorCodes.InvalidParameters);
    }

    [Fact]
    public async Task ValidateAsync_WithAllowedParameters_AppliesOptionalDefault()
    {
        TestContext context = CreateContext();
        Dictionary<string, JsonElement> parameters =
            CreateValidParameters();
        parameters.Remove(GenerationParameterNames.Temperature);
        GenerationRequestMetadataDto metadata = CreateMetadata(parameters);

        Result<StreamingImageGenerationRequest> result =
            await context.Validator.ValidateAsync(
                metadata,
                Array.Empty<IGenerationAttachmentSource>(),
                context.ModelDefinition,
                CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value?.Temperature.Should().Be(
            context.ModelDefinition.Metadata.Temperature.Default);
    }

    private static TestContext CreateContext()
    {
        GenerationModelMetadataDto metadata =
            ApiModelMetadataTestCatalog.LoadNanoBanana2Metadata();
        GenerationModelRules rules = new(
            new IGenerationModelRules[]
            {
                new MetadataGenerationModelRules()
            });
        List<IAttachedImageFormat> formats =
            GenerationImageFileFormats.All
                .Select(descriptor =>
                    (IAttachedImageFormat)new AttachedImageFormat(descriptor))
                .ToList();
        AttachedImageFormatRegistry registry = new(formats);
        MetadataImageModelDefinition modelDefinition = new(metadata);
        StreamingGenerationRequestValidator validator = new(
            registry,
            rules);

        return new TestContext(validator, modelDefinition);
    }

    private static Dictionary<string, JsonElement> CreateValidParameters()
    {
        return new Dictionary<string, JsonElement>(StringComparer.Ordinal)
        {
            [GenerationParameterNames.AspectRatio] =
                JsonSerializer.SerializeToElement("16:9"),
            [GenerationParameterNames.Resolution] =
                JsonSerializer.SerializeToElement("2K"),
            [GenerationParameterNames.Temperature] =
                JsonSerializer.SerializeToElement(1.0),
            [GenerationParameterNames.ThinkingLevel] =
                JsonSerializer.SerializeToElement("low")
        };
    }

    private static GenerationRequestMetadataDto CreateMetadata(
        IReadOnlyDictionary<string, JsonElement> parameters)
    {
        return new GenerationRequestMetadataDto(
            LogicalGenerationId,
            1,
            ApiModelMetadataTestCatalog.NanoBanana2ModelId,
            "Create an image",
            parameters,
            Array.Empty<GenerationAttachmentMetadataDto>());
    }

    private sealed record TestContext(
        StreamingGenerationRequestValidator Validator,
        IImageModelDefinition ModelDefinition);
}
