using SkiaSharp;

namespace AtomicArt.Desktop.Services.Dlss5;

public interface IDlss5RenderScheduler
{
    void Cancel();

    void Request(
        SKBitmap source,
        Dlss5RenderSettings settings,
        long revision,
        Func<long, Dlss5NativeRenderResult, Task> publishResult,
        Func<Exception, Task> reportFailure);
}
