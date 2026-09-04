namespace AtomicArt.Contracts.Generation;

public sealed record ImageGenerationRequestDto(
    string ModelId,
    string Prompt,
    string AspectRatio,
    string Resolution,
    double Temperature,
    int GenerationCount,
    IReadOnlyList<AttachedImageDto> AttachedImages,
    string? ThinkingLevel = null,
    string? Quality = null,
    bool IncludeTemperature = true);
