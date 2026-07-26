using Avalonia;
using Avalonia.Controls;

using AtomicArt.Desktop.Services.Windowing;

namespace AtomicArt.Desktop.Services;

public sealed class WindowStateService :
    IWindowStateService,
    IWindowAttachmentService,
    IWindowPresentationService,
    IDisposable
{
    public bool IsPresented
    {
        get
        {
            lock (_presentationLock)
            {
                return _isPresented;
            }
        }
    }

    private readonly object _presentationLock = new();
    private readonly WindowPlacementTracker _placementTracker;
    private Window? _window;
    private TaskCompletionSource _presentationSource = CreatePresentationSource();
    private bool _isPresented;

    public WindowStateService(WindowPlacementTracker placementTracker)
    {
        _placementTracker = placementTracker
            ?? throw new ArgumentNullException(nameof(placementTracker));
    }

    public void Attach(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);

        if (ReferenceEquals(_window, window))
        {
            UpdatePresentation();
            return;
        }

        if (_window is not null)
        {
            _window.PropertyChanged -= OnWindowPropertyChanged;
        }

        _placementTracker.Attach(window);
        _window = window;
        _window.PropertyChanged += OnWindowPropertyChanged;
        UpdatePresentation();
    }

    public void Minimize()
    {
        if (_window is null)
        {
            return;
        }

        _window.WindowState = WindowState.Minimized;
    }

    public void ToggleWindowState()
    {
        if (_window is null)
        {
            return;
        }

        if (_window.WindowState == WindowState.Maximized)
        {
            _window.WindowState = WindowState.Normal;
            return;
        }

        _window.WindowState = WindowState.Maximized;
    }

    public void ShowAndActivate()
    {
        if (_window is null)
        {
            return;
        }

        _window.Show();
        _window.Activate();
    }

    public Task WaitUntilPresentedAsync(CancellationToken ct)
    {
        Task presentationTask;

        lock (_presentationLock)
        {
            if (_isPresented)
            {
                return Task.CompletedTask;
            }

            presentationTask = _presentationSource.Task;
        }

        return presentationTask.WaitAsync(ct);
    }

    public void Dispose()
    {
        _placementTracker.Dispose();

        if (_window is not null)
        {
            _window.PropertyChanged -= OnWindowPropertyChanged;
            _window = null;
        }

        SetPresentation(false);
    }

    private static TaskCompletionSource CreatePresentationSource()
    {
        return new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private void UpdatePresentation()
    {
        SetPresentation(WindowPresentationState.IsPresented(_window));
    }

    private void SetPresentation(bool isPresented)
    {
        TaskCompletionSource? sourceToComplete = null;

        lock (_presentationLock)
        {
            if (_isPresented == isPresented)
            {
                return;
            }

            _isPresented = isPresented;

            if (isPresented)
            {
                sourceToComplete = _presentationSource;
            }
            else
            {
                _presentationSource = CreatePresentationSource();
            }
        }

        sourceToComplete?.TrySetResult();
    }

    private void OnWindowPropertyChanged(
        object? sender,
        AvaloniaPropertyChangedEventArgs e)
    {
        _ = sender;

        if (e.Property == Visual.IsVisibleProperty
            || e.Property == Window.WindowStateProperty)
        {
            UpdatePresentation();
        }
    }
}
