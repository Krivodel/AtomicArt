using System.Diagnostics;

using Avalonia;
using Avalonia.Threading;

namespace AtomicArt.Desktop.Services.UiAnimation;

internal sealed class PresentationAwareAnimationClock
{
    public TimeSpan Elapsed => _stopwatch.Elapsed;

    private readonly Action _frameAction;
    private readonly Stopwatch _stopwatch = new();
    private readonly DispatcherTimer _timer;
    private readonly TopLevelPresentationObserver _presentationObserver;
    private bool _isActive;
    private bool _isAttached;

    public PresentationAwareAnimationClock(int frameIntervalMilliseconds, Action frameAction)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(frameIntervalMilliseconds, 1);
        _frameAction = frameAction ?? throw new ArgumentNullException(nameof(frameAction));
        _presentationObserver = new TopLevelPresentationObserver(
            OnWindowPresentationChanged);
        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(frameIntervalMilliseconds)
        };
        _timer.Tick += OnTimerTick;
    }

    public void Attach(Visual visual)
    {
        ArgumentNullException.ThrowIfNull(visual);

        _isAttached = true;
        _presentationObserver.Attach(visual);
        if (_isActive && _presentationObserver.IsPresented)
        {
            _stopwatch.Restart();
            _timer.Start();
        }
    }

    public void Detach()
    {
        _isAttached = false;
        _presentationObserver.Detach();
        _timer.Stop();
        _stopwatch.Reset();
    }

    public void SetActive(bool isActive)
    {
        if (_isActive == isActive)
        {
            return;
        }

        _isActive = isActive;
        if (!isActive)
        {
            _timer.Stop();
            _stopwatch.Reset();
            _frameAction();
            return;
        }

        if (_isAttached && _presentationObserver.IsPresented)
        {
            _stopwatch.Restart();
            _timer.Start();
        }
        else
        {
            _stopwatch.Reset();
        }

        _frameAction();
    }

    private void OnTimerTick(object? sender, EventArgs args)
    {
        _ = sender;
        _ = args;

        if (!_presentationObserver.IsPresented)
        {
            _timer.Stop();
            _stopwatch.Stop();
            return;
        }

        _frameAction();
    }

    private void OnWindowPresentationChanged(bool isPresented)
    {
        if (!isPresented)
        {
            _timer.Stop();
            _stopwatch.Stop();
            return;
        }

        if (_isActive && _isAttached)
        {
            _stopwatch.Start();
            _timer.Start();
            _frameAction();
        }
    }
}
