using AtomicArt.Desktop.Models;

namespace AtomicArt.Desktop.Services;

public interface ISettingsDefinitionCatalog
{
    TDefinition GetRequired<TDefinition>()
        where TDefinition : class, ISettingsDefinition;
    IReadOnlyList<ISettingsDefinition> GetSettings();
    IReadOnlyList<UiScaleOption> GetScaleOptions();
}
