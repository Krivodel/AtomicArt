namespace AtomicArt.Contracts.Generation;

public sealed record GenerationModelMetadataDto(
    string Id,
    string DisplayName,
    string Provider,
    string ProviderModelId,
    string PanelId,
    int ContextWindowTokens,
    int MaxOutputTokens,
    int MaxPromptLength,
    IReadOnlyList<string> AspectRatios,
    IReadOnlyList<string> Resolutions,
    IReadOnlyList<int> GenerationCounts,
    GenerationModelTemperatureMetadataDto Temperature,
    GenerationModelAttachmentMetadataDto Attachments,
    GenerationModelPricingMetadataDto Pricing,
    GenerationModelThinkingMetadataDto? Thinking = null);
