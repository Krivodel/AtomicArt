using AtomicArt.Desktop.Models;

namespace AtomicArt.Desktop.Services;

public interface IPromptTextSizeSettingDefinition : IActionSettingDefinition
{
    double DefaultValue { get; }
    IReadOnlyList<NumericSettingOption> Options { get; }
}
