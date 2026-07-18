namespace AtomicArt.Desktop.Services;

public interface IUiThreadDispatcher
{
    Task InvokeAsync(Action action, CancellationToken ct);

    Task InvokeAsync(Func<Task> action, CancellationToken ct);
}
