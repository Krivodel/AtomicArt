namespace AtomicArt.Desktop.Services.State;

public interface IApplicationStateFlushService
{
    Task FlushAsync(IAppStateFlushTarget target, CancellationToken ct);
}
