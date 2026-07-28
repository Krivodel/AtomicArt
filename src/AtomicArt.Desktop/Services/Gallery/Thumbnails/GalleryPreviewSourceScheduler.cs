using Microsoft.Extensions.Options;

using AtomicArt.Desktop.Services.UiAnimation;

namespace AtomicArt.Desktop.Services.Gallery.Thumbnails;

internal sealed class GalleryPreviewSourceScheduler
{
    private readonly IUiFrameScheduler _frameScheduler;
    private readonly Queue<PendingPresentation> _pendingPresentations = [];
    private readonly int _maximumPresentationsPerFrame;
    private bool _frameRequested;

    public GalleryPreviewSourceScheduler(
        IUiFrameScheduler frameScheduler,
        IOptions<GalleryOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _frameScheduler = frameScheduler
            ?? throw new ArgumentNullException(nameof(frameScheduler));
        _maximumPresentationsPerFrame =
            options.Value.MaximumPreviewPresentationsPerFrame;
    }

    public Task PresentAsync(Action present, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(present);
        ct.ThrowIfCancellationRequested();

        PendingPresentation presentation = new(present, ct);
        _pendingPresentations.Enqueue(presentation);
        RequestFrame();

        return presentation.Completion.Task;
    }

    private void RequestFrame()
    {
        if (_frameRequested)
        {
            return;
        }

        _frameRequested = true;
        _frameScheduler.RequestAnimationFrame(OnFrame);
    }

    private void OnFrame(TimeSpan frameTime)
    {
        _ = frameTime;
        _frameRequested = false;
        int presentationCount = 0;

        while ((_pendingPresentations.Count > 0)
            && (presentationCount < _maximumPresentationsPerFrame))
        {
            PendingPresentation presentation = _pendingPresentations.Dequeue();
            presentation.DisposeRegistration();

            if (presentation.Completion.Task.IsCompleted)
            {
                continue;
            }

            presentation.Present();
            presentation.Completion.TrySetResult();
            presentationCount++;
        }

        if (_pendingPresentations.Count > 0)
        {
            RequestFrame();
        }
    }

    private sealed class PendingPresentation
    {
        public TaskCompletionSource Completion { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        private readonly Action _present;
        private readonly CancellationTokenRegistration _cancellationRegistration;

        public PendingPresentation(Action present, CancellationToken ct)
        {
            _present = present;
            _cancellationRegistration = ct.Register(
                static state =>
                {
                    if (state is PendingPresentation presentation)
                    {
                        presentation.Completion.TrySetCanceled();
                    }
                },
                this);
        }

        public void Present()
        {
            _present();
        }

        public void DisposeRegistration()
        {
            _cancellationRegistration.Dispose();
        }
    }
}
