using SkiaSharp;

namespace AtomicArt.Desktop.Services.Dlss5;

public sealed record Dlss5NativeRenderResult(
    SKBitmap Bitmap,
    TimeSpan UploadDuration,
    TimeSpan EvaluateDuration,
    TimeSpan DownloadDuration);
