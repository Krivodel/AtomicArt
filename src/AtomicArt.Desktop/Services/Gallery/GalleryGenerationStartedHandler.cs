using AtomicArt.Desktop.Services.Gallery.State;

namespace AtomicArt.Desktop.Services.Gallery;

public sealed class GalleryGenerationStartedHandler : IGalleryLifecycleEventHandler
{
    public GenerationLifecycleStatus Status => GenerationLifecycleStatus.Started;

    private readonly IGalleryLifecycleViewState _viewState;
    private readonly IGalleryStateService _galleryStateService;

    public GalleryGenerationStartedHandler(
        IGalleryLifecycleViewState viewState,
        IGalleryStateService galleryStateService)
    {
        ArgumentNullException.ThrowIfNull(viewState);
        ArgumentNullException.ThrowIfNull(galleryStateService);

        _viewState = viewState;
        _galleryStateService = galleryStateService;
    }

    public async Task HandleAsync(GenerationLifecycleEvent lifecycleEvent, CancellationToken ct)
    {
        await _viewState.ApplyStartedAsync(lifecycleEvent, ct).ConfigureAwait(false);
        await GalleryStateSnapshotSaver.SaveAsync(
                _viewState,
                _galleryStateService,
                stateSaved: null,
                ct)
            .ConfigureAwait(false);
    }
}
