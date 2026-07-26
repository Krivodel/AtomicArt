namespace AtomicArt.Desktop.Services;

public interface IWindowPresentationService
{
    bool IsPresented { get; }

    Task WaitUntilPresentedAsync(CancellationToken ct);
}
