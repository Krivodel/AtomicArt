using AtomicArt.Application.Common.Models;
using AtomicArt.Contracts.Generation;
using AtomicArt.Domain.Generation;

namespace AtomicArt.Application.Features.Generation.Interfaces;

public interface IImageModelDefinition
{
    string DisplayName { get; }
    string Provider { get; }
    string ProviderModelId { get; }
    string PanelId { get; }
    int ContextWindowTokens { get; }
    int MaxOutputTokens { get; }
    GenerationModelTemperatureMetadataDto Temperature { get; }
    GenerationModelThinkingMetadataDto? Thinking { get; }
    GenerationModelPricingMetadataDto Pricing { get; }
    GenerationModelConstraints Constraints { get; }

    Result<ImageGenerationRequestDto> Validate(ImageGenerationRequestDto request);
}
