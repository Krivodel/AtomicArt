using AtomicArt.Desktop.Services.Gallery.Thumbnails;

namespace AtomicArt.Desktop.Views.Gallery;

internal sealed class GenerationPreviewSession : IGenerationPreviewSession
{
    private readonly IGalleryPreviewBitmapProvider _previewBitmapProvider;
    private readonly GalleryPreviewSourceScheduler _previewSourceScheduler;
    private GenerationPreviewControl? _previewControl;
    private IDisposable? _lifetime;

    public GenerationPreviewSession(
        IGalleryPreviewBitmapProvider previewBitmapProvider,
        GalleryPreviewSourceScheduler previewSourceScheduler)
    {
        _previewBitmapProvider = previewBitmapProvider
            ?? throw new ArgumentNullException(nameof(previewBitmapProvider));
        _previewSourceScheduler = previewSourceScheduler
            ?? throw new ArgumentNullException(nameof(previewSourceScheduler));
    }

    public void Attach(GenerationPreviewControl previewControl)
    {
        ArgumentNullException.ThrowIfNull(previewControl);

        if (_previewControl is not null)
        {
            throw new InvalidOperationException(
                "Generation preview session is already attached.");
        }

        _previewControl = previewControl;
        previewControl.SetPreviewBitmapServices(
            _previewBitmapProvider,
            _previewSourceScheduler);
    }

    public void Dispose()
    {
        GenerationPreviewControl? previewControl = _previewControl;
        IDisposable? lifetime = _lifetime;
        _previewControl = null;
        _lifetime = null;
        previewControl?.ClearPreviewBitmapServices();
        lifetime?.Dispose();
    }

    internal void AttachLifetime(IDisposable lifetime)
    {
        if (_lifetime is not null)
        {
            throw new InvalidOperationException(
                "Generation preview session lifetime is already attached.");
        }

        _lifetime = lifetime
            ?? throw new ArgumentNullException(nameof(lifetime));
    }
}
