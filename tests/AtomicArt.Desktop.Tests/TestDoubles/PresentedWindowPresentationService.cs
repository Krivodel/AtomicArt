using AtomicArt.Desktop.Services;

namespace AtomicArt.Desktop.Tests.TestDoubles;

internal sealed class PresentedWindowPresentationService : IWindowPresentationService
{
    public bool IsPresented => true;

    public Task WaitUntilPresentedAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        return Task.CompletedTask;
    }
}
