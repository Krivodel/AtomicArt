using SkiaSharp;

namespace AtomicArt.Desktop.Services.Dlss5;

public interface IDlss5NativeEngine
{
    bool IsInitialized { get; }

    Task InitializeAsync(IProgress<int>? progress, CancellationToken ct);

    Task PrepareAsync(
        int width,
        int height,
        Dlss5RenderSettings settings,
        SKBitmap? source,
        CancellationToken ct)
    {
        return Task.CompletedTask;
    }

    void Stop();

    Task<Dlss5NativeRenderResult> RenderAsync(
        SKBitmap source,
        Dlss5RenderSettings settings,
        CancellationToken ct);
}
