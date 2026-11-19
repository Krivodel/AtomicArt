using AtomicArt.Desktop.Services.Gallery.State;
using AtomicArt.Desktop.Services.Generation;

namespace AtomicArt.Desktop.Services.Gallery;

public sealed class GalleryGenerationStartedHandler : IGalleryLifecycleEventHandler
{
    public GenerationLifecycleStatus Status => GenerationLifecycleStatus.Started;

    private readonly IGalleryLifecycleViewState _viewState;
    private readonly IGalleryStateService _galleryStateService;
    private readonly IGenerationReferenceStore _referenceStore;

    public GalleryGenerationStartedHandler(
        IGalleryLifecycleViewState viewState,
        IGalleryStateService galleryStateService,
        IGenerationReferenceStore referenceStore)
    {
        ArgumentNullException.ThrowIfNull(viewState);
        ArgumentNullException.ThrowIfNull(galleryStateService);

        _viewState = viewState;
        _galleryStateService = galleryStateService;
        _referenceStore = referenceStore ?? throw new ArgumentNullException(nameof(referenceStore));
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
        _referenceStore.Release(lifecycleEvent.CorrelationId);
    }
}
