namespace AtomicArt.Desktop.ViewModels.Generation;

public sealed record GenerationPanelPreset(
    string ModelId,
    string Prompt,
    string AspectRatio,
    string Resolution);
