namespace AtomicArt.Desktop.Services.State;

public interface IAppStateBootstrapper
{
    Task RestoreAsync(IAppStateRestoreTarget target, CancellationToken ct);

    Task FlushAsync(IAppStateFlushTarget target, CancellationToken ct);
}
