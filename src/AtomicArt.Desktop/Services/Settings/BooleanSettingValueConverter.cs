namespace AtomicArt.Desktop.Services.Settings;

public sealed class BooleanSettingValueConverter : IBooleanSettingValueConverter
{
    public string Format(bool value)
    {
        return value ? bool.TrueString : bool.FalseString;
    }

    public bool TryParse(string value, out bool parsedValue)
    {
        return bool.TryParse(value, out parsedValue);
    }
}
