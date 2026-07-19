using Avalonia.Media.Imaging;

namespace AtomicArt.Desktop.Converters;

public sealed class ImageBytesToBitmapConverter : OneWayImageBitmapConverter
{
    protected override string ConversionDescription => "Image preview conversion";

    protected override Bitmap? ConvertCore(object? value)
    {
        if (value is not byte[] bytes || bytes.Length == 0)
        {
            return null;
        }

        using MemoryStream stream = new(bytes);

        return CreateBitmap(stream);
    }
}
