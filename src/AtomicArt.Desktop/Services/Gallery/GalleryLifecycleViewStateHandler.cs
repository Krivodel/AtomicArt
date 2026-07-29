namespace AtomicArt.Desktop.Services.Gallery;

public abstract class GalleryLifecycleViewStateHandler : IGalleryLifecycleEventHandler
{
    public abstract GenerationLifecycleStatus Status { get; }

    protected IGalleryLifecycleViewState ViewState { get; }

    protected GalleryLifecycleViewStateHandler(IGalleryLifecycleViewState viewState)
    {
        ArgumentNullException.ThrowIfNull(viewState);

        ViewState = viewState;
    }

    public Task HandleAsync(GenerationLifecycleEvent lifecycleEvent, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(lifecycleEvent);

        return ApplyAsync(lifecycleEvent, ct);
    }

    protected abstract Task ApplyAsync(
        GenerationLifecycleEvent lifecycleEvent,
        CancellationToken ct);
}
