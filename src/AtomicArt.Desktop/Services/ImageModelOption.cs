using AtomicArt.Contracts.Generation;

namespace AtomicArt.Desktop.Services;

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
    int MaxAttachedImageBytes,
    long MaxTotalAttachedImageBytes,
    IReadOnlyList<string> SupportedAttachmentContentTypes,
    GenerationModelPricingMetadataDto Pricing,
    GenerationModelThinkingMetadataDto? Thinking = null);
