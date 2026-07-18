using Avalonia.Controls;

namespace AtomicArt.Desktop.Services;

public sealed class WindowStateService : IWindowStateService, IWindowAttachmentService
{
    private Window? _window;

    public void Attach(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);

        _window = window;
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
}
