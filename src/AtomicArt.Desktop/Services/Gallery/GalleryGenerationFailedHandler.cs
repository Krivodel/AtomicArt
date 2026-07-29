using AtomicArt.Desktop.Services.Gallery.State;
using AtomicArt.Desktop.Services.Generation;

namespace AtomicArt.Desktop.Services.Gallery;

public sealed class GalleryGenerationFailedHandler : GalleryLifecycleViewStateHandler
{
    public override GenerationLifecycleStatus Status => GenerationLifecycleStatus.Failed;

    private readonly IGalleryStateService _galleryStateService;

    public GalleryGenerationFailedHandler(
        IGalleryLifecycleViewState viewState,
        IGalleryStateService galleryStateService)
        : base(viewState)
    {
        ArgumentNullException.ThrowIfNull(galleryStateService);

        _galleryStateService = galleryStateService;
    }

    protected override async Task ApplyAsync(
        GenerationLifecycleEvent lifecycleEvent,
        CancellationToken ct)
    {
        string failureCode =
            GenerationFailureCodeResolver.Normalize(lifecycleEvent.FailureCode);

        await ViewState
            .ApplyFailedAsync(
                lifecycleEvent.CorrelationId,
                failureCode,
                ct)
            .ConfigureAwait(false);
        await GalleryStateSnapshotSaver
            .SaveAsync(
                ViewState,
                _galleryStateService,
                stateSaved: null,
                ct)
            .ConfigureAwait(false);
    }
}
