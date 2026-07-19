using System.Globalization;

using Avalonia.Data.Converters;
using Avalonia.Media.Imaging;

namespace AtomicArt.Desktop.Converters;

public abstract class OneWayImageBitmapConverter : IValueConverter
{
    protected abstract string ConversionDescription { get; }

    public object? Convert(
        object? value,
        Type targetType,
        object? parameter,
        CultureInfo culture)
    {
        return ConvertCore(value);
    }

    public object ConvertBack(
        object? value,
        Type targetType,
        object? parameter,
        CultureInfo culture)
    {
        throw new NotSupportedException($"{ConversionDescription} is one-way.");
    }

    protected static Bitmap CreateBitmap(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        return new Bitmap(stream);
    }

    protected abstract Bitmap? ConvertCore(object? value);
}
