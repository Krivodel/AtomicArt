using Avalonia.Media.Imaging;

namespace AtomicArt.Desktop.Converters;

public sealed class ImagePathToBitmapConverter : OneWayImageBitmapConverter
{
    protected override string ConversionDescription => "Image path conversion";

    protected override Bitmap? ConvertCore(object? value)
    {
        if (value is not string path || string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        try
        {
            using FileStream stream = File.OpenRead(path);
            return CreateBitmap(stream);
        }
        catch (ArgumentException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
        catch (NotSupportedException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }
}
