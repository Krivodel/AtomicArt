using AtomicArt.Desktop.Models;

namespace AtomicArt.Desktop.Services;

public sealed class UiScale125OptionDefinition : IUiScaleOptionDefinition
{
    public int Order => 125;
    public UiScaleOption Option => new("125%", 1.25);
}
