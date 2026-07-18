namespace AtomicArt.Desktop.Services.State;

public interface IAppStateFlushTarget
{
    Task CommitPendingStateAsync(CancellationToken ct);
}
