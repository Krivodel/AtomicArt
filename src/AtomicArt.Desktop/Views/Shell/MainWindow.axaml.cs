using System.Runtime.InteropServices;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform;
using Avalonia.VisualTree;
using Pica.Viewer.Services;
using SukiUI.Controls;
using SukiUI.Dialogs;

using AtomicArt.Desktop.Behaviors;
using AtomicArt.Desktop.Controls;
using AtomicArt.Desktop.Controls.Overlays;
using AtomicArt.Desktop.Services;
using AtomicArt.Desktop.Services.Generation;
using AtomicArt.Desktop.ViewModels;
using AtomicArt.Desktop.Views.Dlss5;
using AtomicArt.Desktop.Views.Updates;

namespace AtomicArt.Desktop.Views.Shell;

public partial class MainWindow : SukiWindow
{
    private const int GenerationPanelRowIndex = 1;
    private const string NativeWindowHandleDescriptor = "HWND";
    private const string NonRudeWindowPropertyName = "NonRudeHWND";
    private const string PromptTextBoxName = "PromptTextBox";
    private const string GenerationPanelContextFlyoutKey = "GenerationPanelContextFlyout";

    private RowDefinition GenerationPanelRowDefinition => ShellContentGrid.RowDefinitions[GenerationPanelRowIndex];

    private static readonly nint EnabledWindowPropertyValue = 1;

    private ITrayService? _trayService;
    private IConfirmationDialogPresenter? _confirmationDialogPresenter;
    private ApplicationUpdateToastPresenter? _updateToastPresenter;
    private Dlss5OperationToastPresenter? _dlss5ToastPresenter;
    private bool _isGenerationPanelMinimumHeightInitialized;

    public MainWindow()
    {
        InitializeComponent();
        GenerationPanelContent.AddHandler(
            PointerPressedEvent,
            OnGenerationPanelPointerPressed,
            RoutingStrategies.Tunnel,
            true);
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
        ApplicationUpdateToastPresenter updateToastPresenter,
        Dlss5OperationToastPresenter dlss5ToastPresenter) : this()
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        ArgumentNullException.ThrowIfNull(trayService);
        ArgumentNullException.ThrowIfNull(clipboardImageService);
        ArgumentNullException.ThrowIfNull(dragDropImageService);
        ArgumentNullException.ThrowIfNull(attachmentImageDragService);
        ArgumentNullException.ThrowIfNull(confirmationDialogPresenter);
        ArgumentNullException.ThrowIfNull(dialogManager);
        ArgumentNullException.ThrowIfNull(updateToastPresenter);
        ArgumentNullException.ThrowIfNull(dlss5ToastPresenter);

        _trayService = trayService;
        _confirmationDialogPresenter = confirmationDialogPresenter;
        _updateToastPresenter = updateToastPresenter;
        _dlss5ToastPresenter = dlss5ToastPresenter;
        DataContext = viewModel;
        ConfirmationDialogHost.Manager = dialogManager;
        UpdateToastHost.Manager = updateToastPresenter.Manager;
        updateToastPresenter.Attach(viewModel.ApplicationUpdate);
        dlss5ToastPresenter.Attach(viewModel.Dlss5);
        ClipboardPasteBehavior.SetClipboardImageService(this, clipboardImageService);
        ImageDropBehavior.SetDragDropImageService(this, dragDropImageService);
        AttachmentImageDragBehavior.SetDragService(
            this,
            attachmentImageDragService);
    }

    protected override void OnClosing(WindowClosingEventArgs eventArgs)
    {
        if ((_trayService is not null) && (!_trayService.IsExitRequested))
        {
            eventArgs.Cancel = true;
            _trayService.HideToTray();
        }

        base.OnClosing(eventArgs);
    }

    protected override void OnClosed(EventArgs eventArgs)
    {
        SettingsOverlayPresenter.PropertyChanged -=
            OnSettingsOverlayPresenterPropertyChanged;
        RemoveHandler(KeyDownEvent, OnConfirmationDismissKeyDown);
        _confirmationDialogPresenter?.Dismiss();
        _confirmationDialogPresenter = null;
        _updateToastPresenter?.Dispose();
        _updateToastPresenter = null;
        _dlss5ToastPresenter?.Dispose();
        _dlss5ToastPresenter = null;
        base.OnClosed(eventArgs);
    }

    [DllImport(WindowsNativeLibraryNames.User32, CharSet = CharSet.Unicode, EntryPoint = "RemovePropW", SetLastError = true)]
    private static extern nint RemoveWindowProperty(nint windowHandle, string propertyName);

    [DllImport(WindowsNativeLibraryNames.User32, CharSet = CharSet.Unicode, EntryPoint = "SetPropW", SetLastError = true)]
    private static extern bool SetWindowProperty(nint windowHandle, string propertyName, nint value);

    private static bool IsPromptTextBoxSource(object? source)
    {
        if (source is not Visual visual)
        {
            return false;
        }

        return (string.Equals(
                (visual as TextBox)?.Name,
                PromptTextBoxName,
                StringComparison.Ordinal))
            || (visual.GetVisualAncestors()
                .OfType<TextBox>()
                .Any(textBox => string.Equals(
                    textBox.Name,
                    PromptTextBoxName,
                    StringComparison.Ordinal)));
    }

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
        if ((handle is null)
            || (!string.Equals(handle.HandleDescriptor, NativeWindowHandleDescriptor, StringComparison.Ordinal)))
        {
            return;
        }

        if (WindowState == WindowState.FullScreen)
        {
            _ = MainWindow.RemoveWindowProperty(handle.Handle, NonRudeWindowPropertyName);
            return;
        }

        _ = MainWindow.SetWindowProperty(handle.Handle, NonRudeWindowPropertyName, MainWindow.EnabledWindowPropertyValue);
    }

    private void OnGenerationPanelPointerPressed(
        object? sender,
        PointerPressedEventArgs eventArgs)
    {
        _ = sender;

        if ((eventArgs.GetCurrentPoint(GenerationPanelContent).Properties.PointerUpdateKind
            != PointerUpdateKind.RightButtonPressed)
            || (MainWindow.IsPromptTextBoxSource(eventArgs.Source)))
        {
            return;
        }

        if (GenerationPanelContent.Resources[GenerationPanelContextFlyoutKey]
            is not AnimatedContextMenuFlyout contextFlyout)
        {
            return;
        }

        eventArgs.Handled = true;
        foreach (MenuItem menuItem in contextFlyout.Items.OfType<MenuItem>())
        {
            menuItem.DataContext = DataContext;
        }

        contextFlyout.ShowAt(GenerationPanelContent, true);
    }

    private void OnLoaded(object? sender, RoutedEventArgs eventArgs)
    {
        _ = sender;
        _ = eventArgs;

        InitializeGenerationPanelMinimumHeight();
        if (_isGenerationPanelMinimumHeightInitialized)
        {
            Loaded -= OnLoaded;
        }
    }

    private void OnConfirmationDismissKeyDown(
        object? sender,
        KeyEventArgs eventArgs)
    {
        _ = sender;

        if ((eventArgs.Key == Key.Escape)
            && (_confirmationDialogPresenter is { IsOpen: true }))
        {
            _confirmationDialogPresenter.Dismiss();
            eventArgs.Handled = true;
        }
    }

    private void OnSettingsOverlayPresenterPropertyChanged(
        object? sender,
        AvaloniaPropertyChangedEventArgs eventArgs)
    {
        _ = sender;

        if ((eventArgs.Property == ModalOverlayPresenterControl.IsOpenProperty)
            && (eventArgs.NewValue is false)
            && (IsLoaded))
        {
            FocusPromptInput();
        }
    }

    private void OnWindowPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs eventArgs)
    {
        _ = sender;

        if (eventArgs.Property == WindowStateProperty)
        {
            UpdateWindowsFullscreenDetectionHint();
        }
    }
}
