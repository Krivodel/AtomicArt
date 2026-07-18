using System.Globalization;

using Avalonia.Data.Converters;
using Avalonia.Media.Imaging;

namespace AtomicArt.Desktop.Converters;

public sealed class ImageBytesToBitmapConverter : IValueConverter
{
    public object? Convert(
        object? value,
        Type targetType,
        object? parameter,
        CultureInfo culture)
    {
        if (value is not byte[] bytes || bytes.Length == 0)
        {
            return null;
        }

        using MemoryStream stream = new(bytes);

        return new Bitmap(stream);
    }

    public object ConvertBack(
        object? value,
        Type targetType,
        object? parameter,
        CultureInfo culture)
    {
        throw new NotSupportedException("Image preview conversion is one-way.");
    }
}
