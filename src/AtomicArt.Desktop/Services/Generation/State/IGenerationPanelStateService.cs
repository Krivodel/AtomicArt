namespace AtomicArt.Desktop.Services.Generation.State;

public interface IGenerationPanelStateService
{
    Task<GenerationPanelState> LoadAsync(string panelId, CancellationToken ct);

    Task SaveAsync(string panelId, GenerationPanelState state, CancellationToken ct);
}
