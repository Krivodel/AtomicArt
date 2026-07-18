using AtomicArt.Desktop.Services;

namespace AtomicArt.Desktop.Tests.ViewModels.Gallery;

internal sealed class ImmediateUiThreadDispatcher : IUiThreadDispatcher
{
    public int CallCount { get; private set; }

    public Task InvokeAsync(Action action, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(action);

        ct.ThrowIfCancellationRequested();
        CallCount++;
        action();

        return Task.CompletedTask;
    }

    public async Task InvokeAsync(Func<Task> action, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(action);

        ct.ThrowIfCancellationRequested();
        CallCount++;
        await action();
    }
}
