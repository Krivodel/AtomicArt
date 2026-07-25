namespace AtomicArt.Desktop.Services.Settings;

public interface IDoubleSettingValueConverter
{
    string Format(double value);

    bool TryParse(string value, out double parsedValue);
}
