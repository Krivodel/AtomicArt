using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls;
using Avalonia.Platform;

using CommunityToolkit.Mvvm.Messaging;

using AtomicArt.Desktop.Resources;
using AtomicArt.Desktop.Services.Localization;

namespace AtomicArt.Desktop.Services;

public sealed class TrayService :
    ITrayService,
    ITrayAttachmentService,
    IRecipient<LocalizationChangedMessage>,
    IDisposable
{
    public bool IsExitRequested { get; private set; }

    private static readonly Uri AppIconUri = new("avares://AtomicArt/Assets/AppIcon.ico");
    private readonly IWindowStateService _windowStateService;
    private readonly ILocalizationTextProvider _textProvider;
    private TrayIcon? _trayIcon;
    private NativeMenuItem? _showWindowItem;
    private NativeMenuItem? _exitItem;
    private Window? _window;

    public TrayService(
        IWindowStateService windowStateService,
        ILocalizationTextProvider textProvider,
        IMessenger messenger)
    {
        ArgumentNullException.ThrowIfNull(windowStateService);
        ArgumentNullException.ThrowIfNull(textProvider);
        ArgumentNullException.ThrowIfNull(messenger);

        _windowStateService = windowStateService;
        _textProvider = textProvider;
        messenger.Register<LocalizationChangedMessage>(this);
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
        _windowStateService.Hide();
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

    public void Receive(LocalizationChangedMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

        if (_showWindowItem is not null)
        {
            _showWindowItem.Header = _textProvider.Get(
                ShellLocalizationKeys.ShowWindow);
        }

        if (_exitItem is not null)
        {
            _exitItem.Header = _textProvider.Get(ShellLocalizationKeys.Exit);
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
        _showWindowItem = new NativeMenuItem(
            _textProvider.Get(ShellLocalizationKeys.ShowWindow));
        _showWindowItem.Click += OnShowWindowClicked;
        _exitItem = new NativeMenuItem(
            _textProvider.Get(ShellLocalizationKeys.Exit));
        _exitItem.Click += OnExitClicked;
        NativeMenu menu = [];
        menu.Items.Add(_showWindowItem);
        menu.Items.Add(_exitItem);
        using Stream iconStream = AssetLoader.Open(AppIconUri);
        TrayIcon trayIcon = new()
        {
            Icon = new WindowIcon(iconStream),
            Menu = menu,
            ToolTipText = ProductInformation.Name
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
