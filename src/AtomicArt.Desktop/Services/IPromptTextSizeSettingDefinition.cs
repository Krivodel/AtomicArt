using AtomicArt.Desktop.Models;

namespace AtomicArt.Desktop.Services;

public interface IPromptTextSizeSettingDefinition : IDisplaySettingDefinition
{
    double DefaultValue { get; }
    IReadOnlyList<NumericSettingOption> Options { get; }
}
