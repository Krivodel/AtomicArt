using Avalonia.Media.Imaging;
using SkiaSharp;

namespace AtomicArt.Desktop.Services.Dlss5;

public sealed class Dlss5DisplayImageFactory : IDlss5DisplayImageFactory
{
    public IDlss5DisplayImage Create(SKBitmap bitmap)
    {
        ArgumentNullException.ThrowIfNull(bitmap);

        Bitmap image = Dlss5BitmapPreparation.CreateAvaloniaBitmap(bitmap);
        return new Dlss5DisplayImage(image);
    }
}
