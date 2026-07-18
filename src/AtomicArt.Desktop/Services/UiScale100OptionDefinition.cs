using AtomicArt.Desktop.Models;

namespace AtomicArt.Desktop.Services;

public sealed class UiScale100OptionDefinition : IUiScaleOptionDefinition
{
    public int Order => 100;
    public UiScaleOption Option => new("100%", 1.0);
}
