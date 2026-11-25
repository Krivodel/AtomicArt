using AtomicArt.Desktop.Services.Gallery.Thumbnails;

namespace AtomicArt.Desktop.Views.Gallery;

internal sealed class GenerationPreviewSession : IGenerationPreviewSession
{
    private readonly IGalleryPreviewBitmapProvider _previewBitmapProvider;
    private readonly GalleryPreviewSourceScheduler _previewSourceScheduler;
    private readonly HashSet<GenerationPreviewControl> _previewControls = [];
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

        if (!_previewControls.Add(previewControl))
        {
            throw new InvalidOperationException(
                "Generation preview session is already attached.");
        }

        previewControl.SetPreviewBitmapServices(
            _previewBitmapProvider,
            _previewSourceScheduler);
    }

    public void Detach(GenerationPreviewControl previewControl)
    {
        ArgumentNullException.ThrowIfNull(previewControl);

        if (_previewControls.Remove(previewControl))
        {
            previewControl.ClearPreviewBitmapServices();
        }
    }

    public void Dispose()
    {
        IDisposable? lifetime = _lifetime;
        _lifetime = null;

        foreach (GenerationPreviewControl previewControl in _previewControls)
        {
            previewControl.ClearPreviewBitmapServices();
        }

        _previewControls.Clear();
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
