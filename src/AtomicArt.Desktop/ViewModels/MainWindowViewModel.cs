using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;

using AtomicArt.Desktop.Resources;
using AtomicArt.Desktop.Services;
using AtomicArt.Desktop.Services.Gallery.State;
using AtomicArt.Desktop.Services.Localization;
using AtomicArt.Desktop.Services.Paths;
using AtomicArt.Desktop.Services.State;
using AtomicArt.Desktop.ViewModels.Dialogs;
using AtomicArt.Desktop.ViewModels.Gallery;
using AtomicArt.Desktop.ViewModels.Generation;
using AtomicArt.Desktop.ViewModels.Settings;
using AtomicArt.Desktop.ViewModels.Updates;

namespace AtomicArt.Desktop.ViewModels;

public sealed partial class MainWindowViewModel :
    ObservableObject,
    IAppStateRestoreTarget,
    IAppStateFlushTarget,
    IDataRootMigrationTarget,
    IRecipient<LocalizationChangedMessage>,
    IDisposable
{
    public GalleryViewModel Gallery { get; }
    public ErrorDialogViewModel ErrorDialog { get; }
    public ConfirmationDialogViewModel ConfirmationDialog { get; }
    public IModelPanelViewModel ActiveGenerationPanel { get; }
    public IRelayCommand<string?>? ReplaceGenerationPromptCommand =>
        (ActiveGenerationPanel as IGenerationPromptTarget)?.ReplacePromptCommand;
    public SettingsViewModel Settings => _settings;
    public ApplicationUpdateViewModel ApplicationUpdate { get; }
    public bool HasErrorMessage => !string.IsNullOrWhiteSpace(ErrorMessage);

    private readonly IAppStateBootstrapper _appStateBootstrapper;
    private readonly IReadOnlyList<IAppStateGenerationPanelRestoreTarget> _stateRestorePanels;
    private readonly IReadOnlyList<IAppStateGenerationPanelFlushTarget> _stateFlushPanels;
    private readonly ITrayService _trayService;
    private readonly IUiScaleService _uiScaleService;
    private readonly IWindowStateService _windowStateService;
    private readonly IViewModelErrorHandler _errorHandler;
    private readonly ILocalizationTextProvider _textProvider;
    private readonly SettingsViewModel _settings;
    private string? _errorLocalizationKey;
    [ObservableProperty]
    private bool _isSettingsOpen;
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RestoreAppStateCommand))]
    private bool _isLoading;
    [ObservableProperty]
    private double _uiScale;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasErrorMessage))]
    private string? _errorMessage;

    public MainWindowViewModel(
        GalleryViewModel gallery,
        ErrorDialogViewModel errorDialog,
        ConfirmationDialogViewModel confirmationDialog,
        SettingsViewModel settings,
        IEnumerable<IModelPanelViewModel> modelPanels,
        DesktopModelPanelRegistry desktopModelPanelRegistry,
        IUiScaleService uiScaleService,
        ITrayService trayService,
        IWindowStateService windowStateService,
        IAppStateBootstrapper appStateBootstrapper,
        ApplicationUpdateViewModel applicationUpdate,
        IMessenger messenger,
        IViewModelErrorHandler errorHandler,
        ILocalizationTextProvider textProvider)
    {
        ArgumentNullException.ThrowIfNull(gallery);
        ArgumentNullException.ThrowIfNull(errorDialog);
        ArgumentNullException.ThrowIfNull(confirmationDialog);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(modelPanels);
        ArgumentNullException.ThrowIfNull(desktopModelPanelRegistry);
        ArgumentNullException.ThrowIfNull(uiScaleService);
        ArgumentNullException.ThrowIfNull(trayService);
        ArgumentNullException.ThrowIfNull(windowStateService);
        ArgumentNullException.ThrowIfNull(appStateBootstrapper);
        ArgumentNullException.ThrowIfNull(applicationUpdate);
        ArgumentNullException.ThrowIfNull(messenger);
        ArgumentNullException.ThrowIfNull(errorHandler);
        ArgumentNullException.ThrowIfNull(textProvider);

        IReadOnlyList<IModelPanelViewModel> panels = modelPanels.ToList();
        Gallery = gallery;
        ErrorDialog = errorDialog;
        ConfirmationDialog = confirmationDialog;
        ActiveGenerationPanel = desktopModelPanelRegistry.GetDefaultPanel(panels);
        Gallery.ConfigureImageViewerAttachments(ActiveGenerationPanel.AttachImagesCommand);

        if (ActiveGenerationPanel is IGenerationPanelPresetTarget presetTarget)
        {
            Gallery.ConfigureGenerationPresetTarget(presetTarget);
        }

        _stateRestorePanels = panels
            .OfType<IAppStateGenerationPanelRestoreTarget>()
            .ToList();
        _stateFlushPanels = panels
            .OfType<IAppStateGenerationPanelFlushTarget>()
            .ToList();
        UiScale = uiScaleService.CurrentScale;
        _settings = settings;
        _appStateBootstrapper = appStateBootstrapper;
        _uiScaleService = uiScaleService;
        _trayService = trayService;
        _windowStateService = windowStateService;
        _errorHandler = errorHandler;
        _textProvider = textProvider;
        ApplicationUpdate = applicationUpdate;
        SubscribeToEvents();
        messenger.Register<LocalizationChangedMessage>(this);
    }

    public async Task RestoreGenerationPanelsAsync(CancellationToken ct)
    {
        foreach (IAppStateGenerationPanelRestoreTarget panel in _stateRestorePanels)
        {
            await panel.PrepareStateRestoreAsync(ct);
            await panel.RestoreStateAsync(ct);
        }
    }

    public Task RestoreGalleryAsync(IReadOnlyList<GalleryItemState> items, CancellationToken ct)
    {
        return Gallery.RestoreStateAsync(items, ct);
    }

    public async Task CommitPendingStateAsync(CancellationToken ct)
    {
        foreach (IAppStateGenerationPanelFlushTarget panel in _stateFlushPanels)
        {
            await panel.CommitPendingStateAsync(ct);
        }
    }

    public Task RebaseDataRootAsync(
        string sourceRootDirectory,
        string destinationRootDirectory,
        CancellationToken ct)
    {
        return Gallery.RebaseDataRootAsync(
            sourceRootDirectory,
            destinationRootDirectory,
            ct);
    }

    public void Receive(LocalizationChangedMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

        if (_errorLocalizationKey is not null)
        {
            ErrorMessage = _textProvider.Get(_errorLocalizationKey);
        }
    }

    public void Dispose()
    {
        _settings.CloseRequested -= OnSettingsCloseRequested;
        _settings.Dispose();
        _uiScaleService.ScaleChanged -= OnUiScaleChanged;
        ApplicationUpdate.Dispose();
    }

    [RelayCommand]
    private void OpenSettings()
    {
        IsSettingsOpen = true;
    }

    [RelayCommand]
    private void HideToTray()
    {
        _trayService.HideToTray();
    }

    [RelayCommand]
    private void Minimize()
    {
        _windowStateService.Minimize();
    }

    [RelayCommand]
    private void ToggleWindowState()
    {
        _windowStateService.ToggleWindowState();
    }

    [RelayCommand]
    private void HandleExternalInputError(Exception? exception)
    {
        if (exception is null)
        {
            return;
        }

        _errorHandler.Log(exception, nameof(HandleExternalInputError));
    }

    [RelayCommand(CanExecute = nameof(CanRestoreAppState))]
    private async Task RestoreAppStateAsync(CancellationToken ct)
    {
        await ViewModelAsyncOperation.RunAsync(
            () => _appStateBootstrapper.RestoreAsync(this, ct),
            ct,
            _errorHandler,
            nameof(RestoreAppStateAsync),
            value => IsLoading = value,
            value => ErrorMessage = value,
            value => _errorLocalizationKey = value);
    }

    private void SubscribeToEvents()
    {
        _settings.CloseRequested += OnSettingsCloseRequested;
        _uiScaleService.ScaleChanged += OnUiScaleChanged;
    }

    private bool CanRestoreAppState()
    {
        return !IsLoading;
    }

    private void OnSettingsCloseRequested(object? sender, EventArgs e)
    {
        IsSettingsOpen = false;
    }

    private void OnUiScaleChanged(object? sender, EventArgs e)
    {
        UiScale = _uiScaleService.CurrentScale;
    }
}
