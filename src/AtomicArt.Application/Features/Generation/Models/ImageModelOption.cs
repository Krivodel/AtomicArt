using AtomicArt.Contracts.Generation;

namespace AtomicArt.Application.Features.Generation.Models;

public sealed record ImageModelOption(
    string Id,
    string DisplayName,
    string Provider,
    string ProviderModelId,
    string PanelId,
    int ContextWindowTokens,
    int MaxOutputTokens,
    IReadOnlyList<string> AspectRatios,
    IReadOnlyList<string> Resolutions,
    IReadOnlyList<int> GenerationCounts,
    GenerationModelTemperatureMetadataDto Temperature,
    int MaxAttachedImages,
    int? MaxPromptLength,
    long MaxAttachedImageBytes,
    long MaxTotalAttachedImageBytes,
    IReadOnlyList<string> SupportedContentTypes,
    GenerationModelPricingMetadataDto Pricing,
    GenerationModelThinkingMetadataDto? Thinking = null);
