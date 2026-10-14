using AtomicArt.Desktop.Services.State;

namespace AtomicArt.Desktop.Services.Windowing;

public sealed class GenerationPanelLayoutStateSection : StateSection<GenerationPanelLayoutState>
{
    public const string KeyValue = "generation-panel-layout";
    public const string SectionFileName = "generation-panel-layout.json";

    private const int CurrentSchemaVersion = 1;

    public GenerationPanelLayoutStateSection()
        : base(KeyValue, SectionFileName, CurrentSchemaVersion)
    {
    }

    protected override GenerationPanelLayoutState NormalizePayload(GenerationPanelLayoutState? state)
    {
        return state?.CreateNormalized() ?? new GenerationPanelLayoutState();
    }
}
