namespace AtomicArt.Desktop.Services.Gallery;

public sealed class GalleryGenerationFailedHandler : IGalleryLifecycleEventHandler
{
    public GenerationLifecycleStatus Status => GenerationLifecycleStatus.Failed;

    private readonly IGalleryLifecycleViewState _viewState;

    public GalleryGenerationFailedHandler(IGalleryLifecycleViewState viewState)
    {
        ArgumentNullException.ThrowIfNull(viewState);

        _viewState = viewState;
    }

    public Task HandleAsync(GenerationLifecycleEvent lifecycleEvent, CancellationToken ct)
    {
        return _viewState.ApplyFailedAsync(lifecycleEvent.CorrelationId, ct);
    }
}
