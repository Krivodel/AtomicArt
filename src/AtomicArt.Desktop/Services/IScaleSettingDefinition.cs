namespace AtomicArt.Desktop.Services;

public interface IScaleSettingDefinition : ISettingsDefinition
{
    string DisplayName { get; }
    string ApplyButtonText { get; }
}
