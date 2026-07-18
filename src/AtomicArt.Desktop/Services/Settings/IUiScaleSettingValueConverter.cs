namespace AtomicArt.Desktop.Services.Settings;

public interface IUiScaleSettingValueConverter
{
    string Format(double scale);

    bool TryParse(string value, out double scale);
}
