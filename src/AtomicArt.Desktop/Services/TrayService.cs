using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls;
using Avalonia.Platform;

using AtomicArt.Desktop.Resources;

namespace AtomicArt.Desktop.Services;

public sealed class TrayService : ITrayService, ITrayAttachmentService, IDisposable
{
    private static readonly Uri AppIconUri = new("avares://AtomicArt/Assets/AppIcon.ico");
    private readonly IWindowStateService _windowStateService;
    private TrayIcon? _trayIcon;
    private NativeMenuItem? _showWindowItem;
    private NativeMenuItem? _exitItem;
    private Window? _window;

    public bool IsExitRequested { get; private set; }

    public TrayService(IWindowStateService windowStateService)
    {
        ArgumentNullException.ThrowIfNull(windowStateService);

        _windowStateService = windowStateService;
    }

    public void Attach(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);

        _window = window;

        if (_trayIcon is not null)
        {
            return;
        }

        _trayIcon = CreateTrayIcon();
        _trayIcon.IsVisible = true;
    }

    public void HideToTray()
    {
        _window?.Hide();
    }

    public void ShowWindow()
    {
        _windowStateService.ShowAndActivate();
    }

    public void ExitApplication()
    {
        IsExitRequested = true;

        if (_trayIcon is not null)
        {
            _trayIcon.IsVisible = false;
        }

        if (_window is not null)
        {
            _window.Close();
            return;
        }

        if (Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktopLifetime)
        {
            desktopLifetime.Shutdown();
        }
    }

    public void Dispose()
    {
        if (_trayIcon is not null)
        {
            _trayIcon.Clicked -= OnTrayIconClicked;
            _trayIcon.Dispose();
            _trayIcon = null;
        }

        if (_showWindowItem is not null)
        {
            _showWindowItem.Click -= OnShowWindowClicked;
            _showWindowItem = null;
        }

        if (_exitItem is not null)
        {
            _exitItem.Click -= OnExitClicked;
            _exitItem = null;
        }
    }

    private TrayIcon CreateTrayIcon()
    {
        _showWindowItem = new NativeMenuItem(UiStrings.ShowWindow);
        _showWindowItem.Click += OnShowWindowClicked;
        _exitItem = new NativeMenuItem(UiStrings.Exit);
        _exitItem.Click += OnExitClicked;
        NativeMenu menu = [];
        menu.Items.Add(_showWindowItem);
        menu.Items.Add(_exitItem);
        using Stream iconStream = AssetLoader.Open(AppIconUri);
        TrayIcon trayIcon = new()
        {
            Icon = new WindowIcon(iconStream),
            Menu = menu,
            ToolTipText = UiStrings.AppTitle
        };
        trayIcon.Clicked += OnTrayIconClicked;

        return trayIcon;
    }

    private void OnTrayIconClicked(object? sender, EventArgs e)
    {
        ShowWindow();
    }

    private void OnShowWindowClicked(object? sender, EventArgs e)
    {
        ShowWindow();
    }

    private void OnExitClicked(object? sender, EventArgs e)
    {
        ExitApplication();
    }
}
