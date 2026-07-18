namespace AtomicArt.Desktop.Services.State;

public interface IAppStateGenerationPanelFlushTarget
{
    string PanelId { get; }

    Task CommitPendingStateAsync(CancellationToken ct);
}
