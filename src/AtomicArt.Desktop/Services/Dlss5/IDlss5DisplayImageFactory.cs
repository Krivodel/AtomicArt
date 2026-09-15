using SkiaSharp;

namespace AtomicArt.Desktop.Services.Dlss5;

public interface IDlss5DisplayImageFactory
{
    IDlss5DisplayImage Create(SKBitmap bitmap);
}
