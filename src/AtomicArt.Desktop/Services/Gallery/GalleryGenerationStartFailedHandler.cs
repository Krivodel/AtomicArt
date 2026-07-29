namespace AtomicArt.Desktop.Services.Gallery;

public sealed class GalleryGenerationStartFailedHandler : GalleryLifecycleViewStateHandler
{
    public override GenerationLifecycleStatus Status => GenerationLifecycleStatus.StartFailed;

    public GalleryGenerationStartFailedHandler(IGalleryLifecycleViewState viewState)
        : base(viewState)
    {
    }

    protected override Task ApplyAsync(
        GenerationLifecycleEvent lifecycleEvent,
        CancellationToken ct)
    {
        return ViewState.ApplyStartFailedAsync(lifecycleEvent.CorrelationId, ct);
    }
}
