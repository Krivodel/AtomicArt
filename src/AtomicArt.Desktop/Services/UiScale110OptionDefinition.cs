using AtomicArt.Desktop.Models;

namespace AtomicArt.Desktop.Services;

public sealed class UiScale110OptionDefinition : IUiScaleOptionDefinition
{
    public int Order => 110;
    public UiScaleOption Option => new("110%", 1.1);
}
