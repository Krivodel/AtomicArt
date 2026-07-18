using AtomicArt.Desktop.Resources;

namespace AtomicArt.Desktop.Services;

public sealed class GpuResourceCacheSettingDefinition : IDisplaySettingDefinition
{
    public const string SettingKey = "rendering.gpuResourceCache";

    public string Key => SettingKey;
    public int Order => 300;
    public string DisplayName => UiStrings.SettingsGpuResourceCacheLabel;
    public string SaveButtonText => UiStrings.SettingsSave;
    public string RestartNotice => UiStrings.SettingsGpuResourceCacheRestartNotice;
}
