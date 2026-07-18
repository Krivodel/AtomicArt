using AtomicArt.Desktop.Models;

namespace AtomicArt.Desktop.Services;

public interface IUiScaleOptionDefinition
{
    int Order { get; }
    UiScaleOption Option { get; }
}
