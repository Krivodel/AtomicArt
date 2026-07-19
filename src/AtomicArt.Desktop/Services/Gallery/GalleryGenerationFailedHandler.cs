namespace AtomicArt.Desktop.Services.Gallery;

public sealed class GalleryGenerationFailedHandler : GalleryLifecycleViewStateHandler
{
    public GalleryGenerationFailedHandler(IGalleryLifecycleViewState viewState)
        : base(viewState)
    {
    }

    public override GenerationLifecycleStatus Status => GenerationLifecycleStatus.Failed;

    protected override Task ApplyAsync(Guid correlationId, CancellationToken ct)
    {
        return ViewState.ApplyFailedAsync(correlationId, ct);
    }
}
