using AtomicArt.Contracts.Generation;

namespace AtomicArt.Desktop.Services.Generation;

public sealed record NanoBanana2GenerationParameters(
    ImageModelOption SelectedModel,
    string ModelDisplayName,
    string Prompt,
    string AspectRatio,
    string Resolution,
    double Temperature,
    int GenerationCount,
    IReadOnlyList<AttachedImageDto> AttachedImages,
    string? ThinkingLevel = null);
