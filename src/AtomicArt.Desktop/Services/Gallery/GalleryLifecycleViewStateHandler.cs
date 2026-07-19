namespace AtomicArt.Desktop.Services.Gallery;

public abstract class GalleryLifecycleViewStateHandler : IGalleryLifecycleEventHandler
{
    protected IGalleryLifecycleViewState ViewState { get; }

    protected GalleryLifecycleViewStateHandler(IGalleryLifecycleViewState viewState)
    {
        ArgumentNullException.ThrowIfNull(viewState);

        ViewState = viewState;
    }

    public abstract GenerationLifecycleStatus Status { get; }

    public Task HandleAsync(GenerationLifecycleEvent lifecycleEvent, CancellationToken ct)
    {
        return ApplyAsync(lifecycleEvent.CorrelationId, ct);
    }

    protected abstract Task ApplyAsync(Guid correlationId, CancellationToken ct);
}
