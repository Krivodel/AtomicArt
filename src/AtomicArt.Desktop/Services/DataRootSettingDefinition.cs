using AtomicArt.Desktop.Resources;

namespace AtomicArt.Desktop.Services;

public sealed class DataRootSettingDefinition :
    IDisplaySettingDefinition,
    IExternallyStoredSettingDefinition
{
    public const string KeyValue = "storage.dataRoot";

    public string Key => KeyValue;
    public int Order => 250;
    public string DisplayName => UiStrings.SettingsDataRootLabel;
    public string ActionText => UiStrings.SettingsDataRootChange;
    public string Description => UiStrings.SettingsDataRootDescription;
}
