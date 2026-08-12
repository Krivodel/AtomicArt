using System.Runtime.InteropServices;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform;
using Avalonia.VisualTree;
using SukiUI.Controls;
using SukiUI.Dialogs;

using Pica.Viewer.Services;

using AtomicArt.Desktop.Behaviors;
using AtomicArt.Desktop.Controls.Overlays;
using AtomicArt.Desktop.Services;
using AtomicArt.Desktop.Services.Generation;
using AtomicArt.Desktop.ViewModels;
using AtomicArt.Desktop.Views.Updates;

namespace AtomicArt.Desktop.Views.Shell;

public partial class MainWindow : SukiWindow
{
    private const int GenerationPanelRowIndex = 1;
    private const string NativeWindowHandleDescriptor = "HWND";
    private const string NonRudeWindowPropertyName = "NonRudeHWND";
    private const string PromptTextBoxName = "PromptTextBox";

    private RowDefinition GenerationPanelRowDefinition => ShellContentGrid.RowDefinitions[GenerationPanelRowIndex];

    private static readonly nint EnabledWindowPropertyValue = 1;

    private ITrayService? _trayService;
    private IConfirmationDialogPresenter? _confirmationDialogPresenter;
    private ApplicationUpdateToastPresenter? _updateToastPresenter;
    private bool _isGenerationPanelMinimumHeightInitialized;

    public MainWindow()
    {
        InitializeComponent();
        AttachmentImageDragBehavior.SetDragBoundary(
            GenerationPanelHost,
            GenerationPanelHost);
        AddHandler(
            KeyDownEvent,
            OnConfirmationDismissKeyDown,
            RoutingStrategies.Tunnel,
            true);
        PropertyChanged += OnWindowPropertyChanged;
        SettingsOverlayPresenter.PropertyChanged +=
            OnSettingsOverlayPresenterPropertyChanged;
        UpdateWindowsFullscreenDetectionHint();
        Loaded += OnLoaded;
    }

    public MainWindow(
        MainWindowViewModel viewModel,
        ITrayService trayService,
        IClipboardImageService clipboardImageService,
        IDragDropImageService dragDropImageService,
        IAttachmentImageDragService attachmentImageDragService,
        IConfirmationDialogPresenter confirmationDialogPresenter,
        ISukiDialogManager dialogManager,
        ApplicationUpdateToastPresenter updateToastPresenter) : this()
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        ArgumentNullException.ThrowIfNull(trayService);
        ArgumentNullException.ThrowIfNull(clipboardImageService);
        ArgumentNullException.ThrowIfNull(dragDropImageService);
        ArgumentNullException.ThrowIfNull(attachmentImageDragService);
        ArgumentNullException.ThrowIfNull(confirmationDialogPresenter);
        ArgumentNullException.ThrowIfNull(dialogManager);
        ArgumentNullException.ThrowIfNull(updateToastPresenter);

        _trayService = trayService;
        _confirmationDialogPresenter = confirmationDialogPresenter;
        _updateToastPresenter = updateToastPresenter;
        DataContext = viewModel;
        ConfirmationDialogHost.Manager = dialogManager;
        UpdateToastHost.Manager = updateToastPresenter.Manager;
        updateToastPresenter.Attach(viewModel.ApplicationUpdate);
        ClipboardPasteBehavior.SetClipboardImageService(this, clipboardImageService);
        ImageDropBehavior.SetDragDropImageService(this, dragDropImageService);
        AttachmentImageDragBehavior.SetDragService(
            this,
            attachmentImageDragService);
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
        SettingsOverlayPresenter.PropertyChanged -=
            OnSettingsOverlayPresenterPropertyChanged;
        RemoveHandler(KeyDownEvent, OnConfirmationDismissKeyDown);
        _confirmationDialogPresenter?.Dismiss();
        _confirmationDialogPresenter = null;
        _updateToastPresenter?.Dispose();
        _updateToastPresenter = null;
        base.OnClosed(e);
    }

    [DllImport(WindowsNativeLibraryNames.User32, CharSet = CharSet.Unicode, EntryPoint = "RemovePropW", SetLastError = true)]
    private static extern nint RemoveWindowProperty(nint windowHandle, string propertyName);

    [DllImport(WindowsNativeLibraryNames.User32, CharSet = CharSet.Unicode, EntryPoint = "SetPropW", SetLastError = true)]
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

    private void FocusPromptInput()
    {
        TextBox? promptInput = this
            .GetVisualDescendants()
            .OfType<TextBox>()
            .SingleOrDefault(textBox =>
                string.Equals(
                    textBox.Name,
                    PromptTextBoxName,
                    StringComparison.Ordinal));

        if (promptInput is not null)
        {
            TextBoxFocusBehavior.RequestFocus(promptInput);
        }
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

    private void OnConfirmationDismissKeyDown(
        object? sender,
        KeyEventArgs e)
    {
        _ = sender;

        if (e.Key == Key.Escape
            && _confirmationDialogPresenter is { IsOpen: true })
        {
            _confirmationDialogPresenter.Dismiss();
            e.Handled = true;
        }
    }

    private void OnSettingsOverlayPresenterPropertyChanged(
        object? sender,
        AvaloniaPropertyChangedEventArgs e)
    {
        _ = sender;

        if (e.Property == ModalOverlayPresenterControl.IsOpenProperty
            && e.NewValue is false
            && IsLoaded)
        {
            FocusPromptInput();
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
