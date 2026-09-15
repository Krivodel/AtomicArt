namespace AtomicArt.Desktop.Services.Dlss5;

internal readonly record struct Dlss5RenderCacheKey(
    long SourceGeneration,
    int Width,
    int Height,
    Dlss5RenderSettings Settings);
