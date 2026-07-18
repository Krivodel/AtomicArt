using AtomicArt.Application.Common.Models;
using AtomicArt.Contracts.Generation;
using AtomicArt.Domain.Generation;

namespace AtomicArt.Application.Features.Generation.Interfaces;

public interface IImageModelDefinition
{
    string Id { get; }
    string DisplayName { get; }
    string Provider { get; }
    string ProviderModelId { get; }
    string PanelId { get; }
    int ContextWindowTokens { get; }
    int MaxOutputTokens { get; }
    int MaxAttachedImages { get; }
    int? MaxPromptLength { get; }
    long MaxAttachedImageBytes { get; }
    long MaxTotalAttachedImageBytes { get; }
    GenerationModelTemperatureMetadataDto Temperature { get; }
    GenerationModelThinkingMetadataDto? Thinking { get; }
    GenerationModelPricingMetadataDto Pricing { get; }
    GenerationModelConstraints Constraints { get; }

    IReadOnlyList<string> GetAspectRatios();

    IReadOnlyList<string> GetResolutions();

    IReadOnlyList<int> GetGenerationCounts();

    IReadOnlyList<string> GetSupportedContentTypes();

    Result<ImageGenerationRequestDto> Validate(ImageGenerationRequestDto request);
}
