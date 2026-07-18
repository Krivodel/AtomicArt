using AtomicArt.Desktop.Models;

namespace AtomicArt.Desktop.Services;

public sealed class SettingsDefinitionCatalog : ISettingsDefinitionCatalog
{
    private readonly IReadOnlyDictionary<Type, ISettingsDefinition> _settingsByType;
    private readonly IReadOnlyList<ISettingsDefinition> _settings;
    private readonly IReadOnlyList<UiScaleOption> _scaleOptions;

    public SettingsDefinitionCatalog(
        IEnumerable<ISettingsDefinition> settings,
        IEnumerable<IUiScaleOptionDefinition> scaleOptions)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(scaleOptions);

        _settings = settings
            .OrderBy(setting => setting.Order)
            .ThenBy(setting => setting.Key, StringComparer.Ordinal)
            .ToList();
        _settingsByType = _settings.ToDictionary(setting => setting.GetType());
        _scaleOptions = scaleOptions
            .OrderBy(option => option.Order)
            .Select(option => option.Option)
            .ToList();
    }

    public TDefinition GetRequired<TDefinition>()
        where TDefinition : class, ISettingsDefinition
    {
        if (_settingsByType.TryGetValue(typeof(TDefinition), out ISettingsDefinition? definition)
            && definition is TDefinition typedDefinition)
        {
            return typedDefinition;
        }

        throw new InvalidOperationException($"Settings definition '{typeof(TDefinition).Name}' is not registered.");
    }

    public IReadOnlyList<ISettingsDefinition> GetSettings()
    {
        return _settings;
    }

    public IReadOnlyList<UiScaleOption> GetScaleOptions()
    {
        return _scaleOptions;
    }
}
