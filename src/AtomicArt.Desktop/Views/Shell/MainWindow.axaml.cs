using System.Runtime.InteropServices;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform;
using SukiUI.Controls;

using AtomicArt.Desktop.Behaviors;
using AtomicArt.Desktop.Services;
using AtomicArt.Desktop.ViewModels;
using AtomicArt.Desktop.Views.Updates;

namespace AtomicArt.Desktop.Views.Shell;

public partial class MainWindow : SukiWindow
{
    private const int GenerationPanelRowIndex = 1;
    private const string NativeWindowHandleDescriptor = "HWND";
    private const string NonRudeWindowPropertyName = "NonRudeHWND";

    private RowDefinition GenerationPanelRowDefinition => ShellContentGrid.RowDefinitions[GenerationPanelRowIndex];

    private static readonly nint EnabledWindowPropertyValue = 1;

    private ITrayService? _trayService;
    private ApplicationUpdateToastPresenter? _updateToastPresenter;
    private bool _isGenerationPanelMinimumHeightInitialized;

    public MainWindow()
    {
        InitializeComponent();
        PropertyChanged += OnWindowPropertyChanged;
        UpdateWindowsFullscreenDetectionHint();
        Loaded += OnLoaded;
    }

    public MainWindow(
        MainWindowViewModel viewModel,
        ITrayService trayService,
        IClipboardImageService clipboardImageService,
        IDragDropImageService dragDropImageService,
        ApplicationUpdateToastPresenter updateToastPresenter) : this()
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        ArgumentNullException.ThrowIfNull(trayService);
        ArgumentNullException.ThrowIfNull(clipboardImageService);
        ArgumentNullException.ThrowIfNull(dragDropImageService);
        ArgumentNullException.ThrowIfNull(updateToastPresenter);

        _trayService = trayService;
        _updateToastPresenter = updateToastPresenter;
        DataContext = viewModel;
        UpdateToastHost.Manager = updateToastPresenter.Manager;
        updateToastPresenter.Attach(viewModel.ApplicationUpdate);
        ClipboardPasteBehavior.SetClipboardImageService(this, clipboardImageService);
        ImageDropBehavior.SetDragDropImageService(this, dragDropImageService);
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        if (_trayService is not null && !_trayService.IsExitRequested)
        {
            e.Cancel = true;
            _trayService.HideToTray();
        }

        base.OnClosing(e);
    }

    protected override void OnClosed(EventArgs e)
    {
        _updateToastPresenter?.Dispose();
        _updateToastPresenter = null;
        base.OnClosed(e);
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "RemovePropW", SetLastError = true)]
    private static extern nint RemoveWindowProperty(nint windowHandle, string propertyName);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "SetPropW", SetLastError = true)]
    private static extern bool SetWindowProperty(nint windowHandle, string propertyName, nint value);

    private void InitializeGenerationPanelMinimumHeight()
    {
        if (_isGenerationPanelMinimumHeightInitialized)
        {
            return;
        }

        RowDefinition generationPanelRow = GenerationPanelRowDefinition;
        double generationPanelHeight = generationPanelRow.ActualHeight;
        if (generationPanelHeight <= 0d)
        {
            generationPanelHeight = GenerationPanelHost.Bounds.Height;
        }

        if (generationPanelHeight <= 0d)
        {
            return;
        }

        generationPanelRow.MinHeight = generationPanelHeight;
        _isGenerationPanelMinimumHeightInitialized = true;
    }

    private void UpdateWindowsFullscreenDetectionHint()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        IPlatformHandle? handle = TryGetPlatformHandle();
        if (handle is null
            || !string.Equals(handle.HandleDescriptor, NativeWindowHandleDescriptor, StringComparison.Ordinal))
        {
            return;
        }

        if (WindowState == WindowState.FullScreen)
        {
            _ = RemoveWindowProperty(handle.Handle, NonRudeWindowPropertyName);
            return;
        }

        _ = SetWindowProperty(handle.Handle, NonRudeWindowPropertyName, EnabledWindowPropertyValue);
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;

        InitializeGenerationPanelMinimumHeight();
        if (_isGenerationPanelMinimumHeightInitialized)
        {
            Loaded -= OnLoaded;
        }
    }

    private void OnWindowPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        _ = sender;

        if (e.Property == WindowStateProperty)
        {
            UpdateWindowsFullscreenDetectionHint();
        }
    }
}
