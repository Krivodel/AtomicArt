using Avalonia.Threading;

namespace AtomicArt.Desktop.Services;

public sealed class AvaloniaUiThreadDispatcher : IUiThreadDispatcher
{
    public async Task InvokeAsync(Action action, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(action);

        await Dispatcher.UIThread.InvokeAsync(action, DispatcherPriority.Default, ct);
    }

    public async Task InvokeAsync(Func<Task> action, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(action);

        Task operationTask = await Dispatcher.UIThread.InvokeAsync(
            action,
            DispatcherPriority.Default,
            ct);
        await operationTask;
    }
}
