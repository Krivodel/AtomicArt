namespace AtomicArt.Desktop.Services.Localization;

public interface ILocalizationTextProvider
{
    string Get(string key);

    string Format(string key, params object?[] arguments);
}
