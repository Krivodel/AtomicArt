using System.Globalization;

namespace AtomicArt.Desktop.Services.Settings;

public sealed class DoubleSettingValueConverter : IDoubleSettingValueConverter
{
    public string Format(double value)
    {
        return value.ToString("R", CultureInfo.InvariantCulture);
    }

    public bool TryParse(string value, out double parsedValue)
    {
        return double.TryParse(
            value,
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out parsedValue);
    }
}
