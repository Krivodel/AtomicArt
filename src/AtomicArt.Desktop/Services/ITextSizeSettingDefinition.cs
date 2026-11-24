using AtomicArt.Desktop.Models;

namespace AtomicArt.Desktop.Services;

public interface ITextSizeSettingDefinition : IDisplaySettingDefinition
{
    double DefaultValue { get; }
    IReadOnlyList<NumericSettingOption> Options { get; }
}
