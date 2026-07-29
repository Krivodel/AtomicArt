using Avalonia.Controls;

namespace AtomicArt.Desktop.Services.UiAnimation;

internal sealed class AvaloniaUiFrameScheduler : IUiFrameScheduler
{
    private readonly TopLevel _topLevel;
    private readonly TopLevelPresentationObserver _presentationObserver;
    private readonly Action<Action<TimeSpan>> _requestAnimationFrame;
    private readonly List<PendingFrameRequest> _pendingRequests = [];
    private bool _isPresented;

    public AvaloniaUiFrameScheduler(TopLevel topLevel)
        : this(
            topLevel,
            frameAction => topLevel.RequestAnimationFrame(frameAction))
    {
    }

    internal AvaloniaUiFrameScheduler(
        TopLevel topLevel,
        Action<Action<TimeSpan>> requestAnimationFrame)
    {
        _topLevel = topLevel ?? throw new ArgumentNullException(nameof(topLevel));
        _presentationObserver = new TopLevelPresentationObserver(
            OnPresentationChanged);
        _requestAnimationFrame = requestAnimationFrame
            ?? throw new ArgumentNullException(nameof(requestAnimationFrame));
    }

    public void RequestAnimationFrame(Action<TimeSpan> frameAction)
    {
        ArgumentNullException.ThrowIfNull(frameAction);

        EnsurePresentationObserverAttached();
        SetPresentation(_presentationObserver.IsPresented);

        PendingFrameRequest request = new(frameAction);
        _pendingRequests.Add(request);

        if (_isPresented)
        {
            Submit(request);
        }
    }

    private void EnsurePresentationObserverAttached()
    {
        if (!_presentationObserver.IsAttached)
        {
            _presentationObserver.Attach(_topLevel);
        }
    }

    private void OnPresentationChanged(bool isPresented)
    {
        SetPresentation(isPresented);
    }

    private void SetPresentation(bool isPresented)
    {
        if (_isPresented == isPresented)
        {
            return;
        }

        _isPresented = isPresented;

        if (!_isPresented)
        {
            foreach (PendingFrameRequest request in _pendingRequests.ToList())
            {
                request.InvalidateSubmission();
            }

            return;
        }

        foreach (PendingFrameRequest request in _pendingRequests.ToList())
        {
            Submit(request);
        }
    }

    private void Submit(PendingFrameRequest request)
    {
        int submissionVersion = request.BeginSubmission();

        _requestAnimationFrame(
            frameTime => Complete(
                request,
                submissionVersion,
                frameTime));
    }

    private void Complete(
        PendingFrameRequest request,
        int submissionVersion,
        TimeSpan frameTime)
    {
        if (!request.TryComplete(submissionVersion))
        {
            return;
        }

        _pendingRequests.Remove(request);

        if (_pendingRequests.Count == 0)
        {
            _presentationObserver.Detach();
            _isPresented = false;
        }

        request.FrameAction(frameTime);
    }

    private sealed class PendingFrameRequest
    {
        public Action<TimeSpan> FrameAction { get; }

        private int _submissionVersion;
        private bool _isCompleted;

        public PendingFrameRequest(Action<TimeSpan> frameAction)
        {
            FrameAction = frameAction
                ?? throw new ArgumentNullException(nameof(frameAction));
        }

        public int BeginSubmission()
        {
            return ++_submissionVersion;
        }

        public void InvalidateSubmission()
        {
            _submissionVersion++;
        }

        public bool TryComplete(int submissionVersion)
        {
            if (_isCompleted || submissionVersion != _submissionVersion)
            {
                return false;
            }

            _isCompleted = true;

            return true;
        }
    }
}
