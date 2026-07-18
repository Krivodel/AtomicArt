namespace AtomicArt.Desktop.Services.Generation.State;

public sealed class GenerationPanelsState
{
    public Dictionary<string, GenerationPanelState> Panels { get; init; } = new(StringComparer.Ordinal);
}
