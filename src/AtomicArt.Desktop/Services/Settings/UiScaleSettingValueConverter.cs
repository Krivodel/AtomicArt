using System.Globalization;

namespace AtomicArt.Desktop.Services.Settings;

public sealed class UiScaleSettingValueConverter : IUiScaleSettingValueConverter
{
    public string Format(double scale)
    {
        return scale.ToString("R", CultureInfo.InvariantCulture);
    }

    public bool TryParse(string value, out double scale)
    {
        return double.TryParse(
            value,
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out scale);
    }
}
