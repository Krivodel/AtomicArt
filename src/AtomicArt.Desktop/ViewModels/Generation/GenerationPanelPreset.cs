using AtomicArt.Contracts.Generation;
using AtomicArt.Desktop.Services.Generation;

namespace AtomicArt.Desktop.ViewModels.Generation;

public sealed record GenerationPanelPreset(
    string ModelId,
    string Prompt,
    string AspectRatio,
    string Resolution,
    GenerationRequestSettings? RequestSettings = null,
    IReadOnlyList<AttachedImageDto>? References = null);
