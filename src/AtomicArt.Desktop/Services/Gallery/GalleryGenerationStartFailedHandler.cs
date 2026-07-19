namespace AtomicArt.Desktop.Services.Gallery;

public sealed class GalleryGenerationStartFailedHandler : GalleryLifecycleViewStateHandler
{
    public GalleryGenerationStartFailedHandler(IGalleryLifecycleViewState viewState)
        : base(viewState)
    {
    }

    public override GenerationLifecycleStatus Status => GenerationLifecycleStatus.StartFailed;

    protected override Task ApplyAsync(Guid correlationId, CancellationToken ct)
    {
        return ViewState.ApplyStartFailedAsync(correlationId, ct);
    }
}
