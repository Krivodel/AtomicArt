namespace AtomicArt.Desktop.Services.Settings;

public interface ISettingsStateApplicator
{
    string SettingKey { get; }

    void Apply(string value);
}
