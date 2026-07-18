using AtomicArt.Desktop.Services;

namespace AtomicArt.Desktop.ViewModels.Settings;

public sealed class SettingsItemViewModelProvider : ISettingsItemViewModelProvider
{
    private readonly ISettingsDefinitionCatalog _settingsDefinitionCatalog;
    private readonly IReadOnlyList<ISettingsItemViewModelFactory> _settingFactories;

    public SettingsItemViewModelProvider(
        ISettingsDefinitionCatalog settingsDefinitionCatalog,
        IEnumerable<ISettingsItemViewModelFactory> settingFactories)
    {
        ArgumentNullException.ThrowIfNull(settingsDefinitionCatalog);
        ArgumentNullException.ThrowIfNull(settingFactories);

        _settingsDefinitionCatalog = settingsDefinitionCatalog;
        _settingFactories = settingFactories.ToList();
    }

    public IReadOnlyList<ISettingItemViewModel> CreateSettings()
    {
        return _settingsDefinitionCatalog
            .GetSettings()
            .Select(CreateSetting)
            .OrderBy(setting => setting.Order)
            .ThenBy(setting => setting.Key, StringComparer.Ordinal)
            .ToList();
    }

    private ISettingItemViewModel CreateSetting(ISettingsDefinition setting)
    {
        ISettingsItemViewModelFactory? factory = _settingFactories
            .FirstOrDefault(currentFactory => currentFactory.CanCreate(setting));

        if (factory is null)
        {
            throw new InvalidOperationException(
                $"Settings item factory for '{setting.GetType().Name}' is not registered.");
        }

        return factory.Create(setting);
    }
}
