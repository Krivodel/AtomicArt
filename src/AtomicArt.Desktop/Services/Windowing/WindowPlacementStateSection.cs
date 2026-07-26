using AtomicArt.Desktop.Services.State;

namespace AtomicArt.Desktop.Services.Windowing;

public sealed class WindowPlacementStateSection :
    StateSection<WindowPlacementState>
{
    public const string KeyValue = "window";
    public const string SectionFileName = "window.json";

    private const int CurrentSchemaVersion = 1;

    public WindowPlacementStateSection()
        : base(KeyValue, SectionFileName, CurrentSchemaVersion)
    {
    }

    protected override WindowPlacementState NormalizePayload(
        WindowPlacementState? state)
    {
        return state?.CreateNormalized()
            ?? new WindowPlacementState();
    }
}
