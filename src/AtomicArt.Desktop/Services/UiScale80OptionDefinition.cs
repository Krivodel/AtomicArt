using AtomicArt.Desktop.Models;

namespace AtomicArt.Desktop.Services;

public sealed class UiScale80OptionDefinition : IUiScaleOptionDefinition
{
    public int Order => 80;
    public UiScaleOption Option => new("80%", 0.8);
}
