namespace AtomicArt.Desktop.Services.Gallery;

public sealed class GalleryGenerationStartFailedHandler : IGalleryLifecycleEventHandler
{
    public GenerationLifecycleStatus Status => GenerationLifecycleStatus.StartFailed;

    private readonly IGalleryLifecycleViewState _viewState;

    public GalleryGenerationStartFailedHandler(IGalleryLifecycleViewState viewState)
    {
        ArgumentNullException.ThrowIfNull(viewState);

        _viewState = viewState;
    }

    public Task HandleAsync(GenerationLifecycleEvent lifecycleEvent, CancellationToken ct)
    {
        return _viewState.ApplyStartFailedAsync(lifecycleEvent.CorrelationId, ct);
    }
}
