using AtomicArt.Desktop.Models;

namespace AtomicArt.Desktop.Services;

public sealed class UiScale150OptionDefinition : IUiScaleOptionDefinition
{
    public int Order => 150;
    public UiScaleOption Option => new("150%", 1.5);
}
