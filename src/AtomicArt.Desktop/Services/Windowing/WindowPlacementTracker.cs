using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;

using Microsoft.Extensions.Logging;

using AtomicArt.Desktop.Services.State;

namespace AtomicArt.Desktop.Services.Windowing;

public sealed class WindowPlacementTracker : IDisposable
{
    private readonly IAppStateStore _stateStore;
    private readonly IStateWriteScheduler _writeScheduler;
    private readonly WindowPlacementStateSection _stateSection;
    private readonly ILogger<WindowPlacementTracker> _logger;

    private Window? _window;
    private PixelPoint? _normalPosition;
    private Size? _normalClientSize;
    private bool _isMaximized;
    private bool _isTracking;

    public WindowPlacementTracker(
        IAppStateStore stateStore,
        IStateWriteScheduler writeScheduler,
        WindowPlacementStateSection stateSection,
        ILogger<WindowPlacementTracker> logger)
    {
        _stateStore = stateStore
            ?? throw new ArgumentNullException(nameof(stateStore));
        _writeScheduler = writeScheduler
            ?? throw new ArgumentNullException(nameof(writeScheduler));
        _stateSection = stateSection
            ?? throw new ArgumentNullException(nameof(stateSection));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public void Attach(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);

        if (ReferenceEquals(_window, window))
        {
            return;
        }

        Detach();
        _window = window;
        ApplySavedPlacement(window, LoadState());
        window.Opened += OnWindowOpened;
        window.PositionChanged += OnWindowPositionChanged;
        window.Resized += OnWindowResized;
        window.PropertyChanged += OnWindowPropertyChanged;

        if (window.IsVisible)
        {
            StartTracking(window);
        }
    }

    public void Dispose()
    {
        Detach();
    }

    private WindowPlacementState LoadState()
    {
        try
        {
            return _stateStore
                .LoadAsync<WindowPlacementState>(
                    _stateSection,
                    CancellationToken.None)
                .GetAwaiter()
                .GetResult();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to restore the main window placement.");

            return new WindowPlacementState();
        }
    }

    private void ApplySavedPlacement(
        Window window,
        WindowPlacementState state)
    {
        Screen? targetScreen = WindowPlacementResolver.FindTargetScreen(
            window,
            state);
        Size? restoredSize = WindowPlacementResolver.ResolveSize(
            window,
            state,
            targetScreen);

        if (restoredSize is { } size)
        {
            window.Width = size.Width;
            window.Height = size.Height;
            _normalClientSize = size;
        }

        PixelPoint? restoredPosition = WindowPlacementResolver.ResolvePosition(
            state,
            restoredSize
                ?? WindowPlacementResolver.GetCurrentWindowSize(window),
            targetScreen);

        if (restoredPosition is { } position)
        {
            window.WindowStartupLocation = WindowStartupLocation.Manual;
            window.Position = position;
            _normalPosition = position;
        }
        else if (WindowPlacementResolver.HasSavedGeometry(state))
        {
            window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        }

        _isMaximized = state.IsMaximized;

        if (_isMaximized)
        {
            window.WindowState = WindowState.Maximized;
        }
    }

    private void OnWindowOpened(object? sender, EventArgs e)
    {
        _ = e;

        if (sender is Window window)
        {
            StartTracking(window);
        }
    }

    private void StartTracking(Window window)
    {
        if (_isTracking)
        {
            return;
        }

        _isTracking = true;

        if (window.WindowState == WindowState.Normal)
        {
            CaptureNormalPlacement(
                window.Position,
                window.ClientSize,
                false);
        }
    }

    private void OnWindowPositionChanged(
        object? sender,
        PixelPointEventArgs e)
    {
        if (sender is Window window
            && _isTracking
            && window.WindowState == WindowState.Normal)
        {
            CaptureNormalPlacement(
                e.Point,
                window.ClientSize,
                true);
        }
    }

    private void OnWindowResized(
        object? sender,
        WindowResizedEventArgs e)
    {
        if (sender is Window window
            && _isTracking
            && window.WindowState == WindowState.Normal)
        {
            CaptureNormalPlacement(
                window.Position,
                e.ClientSize,
                true);
        }
    }

    private void OnWindowPropertyChanged(
        object? sender,
        AvaloniaPropertyChangedEventArgs e)
    {
        if (sender is not Window window
            || !_isTracking
            || e.Property != Window.WindowStateProperty)
        {
            return;
        }

        if (window.WindowState == WindowState.Maximized)
        {
            _isMaximized = true;
            ScheduleStateWrite();
        }
        else if (window.WindowState == WindowState.Normal)
        {
            _isMaximized = false;
            ScheduleStateWrite();
        }
    }

    private void CaptureNormalPlacement(
        PixelPoint position,
        Size clientSize,
        bool scheduleWrite)
    {
        _normalPosition = position;

        if (clientSize.Width > 0d
            && clientSize.Height > 0d
            && double.IsFinite(clientSize.Width)
            && double.IsFinite(clientSize.Height))
        {
            _normalClientSize = clientSize;
        }

        _isMaximized = false;

        if (scheduleWrite)
        {
            ScheduleStateWrite();
        }
    }

    private void ScheduleStateWrite()
    {
        WindowPlacementState state = new()
        {
            X = _normalPosition?.X,
            Y = _normalPosition?.Y,
            Width = _normalClientSize?.Width,
            Height = _normalClientSize?.Height,
            IsMaximized = _isMaximized
        };

        _writeScheduler.ScheduleWrite(
            _stateSection,
            state,
            StateWriteMode.Deferred);
    }

    private void Detach()
    {
        if (_window is not null)
        {
            _window.Opened -= OnWindowOpened;
            _window.PositionChanged -= OnWindowPositionChanged;
            _window.Resized -= OnWindowResized;
            _window.PropertyChanged -= OnWindowPropertyChanged;
            _window = null;
        }

        _normalPosition = null;
        _normalClientSize = null;
        _isMaximized = false;
        _isTracking = false;
    }
}
