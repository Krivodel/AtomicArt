namespace AtomicArt.Desktop.Services.State;

public interface IAppStateGenerationPanelRestoreTarget
{
    string PanelId { get; }

    Task PrepareStateRestoreAsync(CancellationToken ct);

    Task RestoreStateAsync(CancellationToken ct);
}
