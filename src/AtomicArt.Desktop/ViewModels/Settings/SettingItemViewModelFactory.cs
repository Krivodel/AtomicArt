using AtomicArt.Desktop.Services;

namespace AtomicArt.Desktop.ViewModels.Settings;

public abstract class SettingItemViewModelFactory<TDefinition> : ISettingsItemViewModelFactory
    where TDefinition : ISettingsDefinition
{
    private readonly string _invalidDefinitionMessage;

    protected SettingItemViewModelFactory(string invalidDefinitionMessage)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(invalidDefinitionMessage);

        _invalidDefinitionMessage = invalidDefinitionMessage;
    }

    public bool CanCreate(ISettingsDefinition definition)
    {
        return definition is TDefinition;
    }

    public ISettingItemViewModel Create(ISettingsDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        if (definition is not TDefinition typedDefinition)
        {
            throw new InvalidOperationException(_invalidDefinitionMessage);
        }

        return CreateItemViewModel(typedDefinition);
    }

    protected abstract ISettingItemViewModel CreateItemViewModel(TDefinition definition);
}
