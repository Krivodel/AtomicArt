namespace AtomicArt.Desktop.Services.Settings;

public interface IBooleanSettingValueConverter
{
    string Format(bool value);

    bool TryParse(string value, out bool parsedValue);
}
