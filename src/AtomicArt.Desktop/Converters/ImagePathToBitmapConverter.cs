using System.Globalization;

using Avalonia.Data.Converters;
using Avalonia.Media.Imaging;

namespace AtomicArt.Desktop.Converters;

public sealed class ImagePathToBitmapConverter : IValueConverter
{
    public object? Convert(
        object? value,
        Type targetType,
        object? parameter,
        CultureInfo culture)
    {
        if (value is not string path || string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        try
        {
            using FileStream stream = File.OpenRead(path);
            
            return new Bitmap(stream);
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

    public object ConvertBack(
        object? value,
        Type targetType,
        object? parameter,
        CultureInfo culture)
    {
        throw new NotSupportedException("Image path conversion is one-way.");
    }
}
