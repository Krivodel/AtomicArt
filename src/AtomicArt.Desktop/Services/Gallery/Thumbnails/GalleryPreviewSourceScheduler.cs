using AtomicArt.Desktop.Services.UiAnimation;

namespace AtomicArt.Desktop.Services.Gallery.Thumbnails;

internal sealed class GalleryPreviewSourceScheduler
{
    private const int MaximumPresentationsPerFrame = 1;

    private readonly IUiFrameScheduler _frameScheduler;
    private readonly Queue<PendingPresentation> _pendingPresentations = [];
    private bool _frameRequested;

    public GalleryPreviewSourceScheduler(IUiFrameScheduler frameScheduler)
    {
        _frameScheduler = frameScheduler
            ?? throw new ArgumentNullException(nameof(frameScheduler));
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
            && (presentationCount < MaximumPresentationsPerFrame))
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
