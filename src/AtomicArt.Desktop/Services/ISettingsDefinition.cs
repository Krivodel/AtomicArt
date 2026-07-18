namespace AtomicArt.Desktop.Services;

public interface ISettingsDefinition
{
    string Key { get; }
    int Order { get; }
}
