using AtomicArt.Desktop.Models;

namespace AtomicArt.Desktop.Services;

public sealed class UiScale60OptionDefinition : IUiScaleOptionDefinition
{
    public int Order => 60;
    public UiScaleOption Option => new("60%", 0.6);
}
