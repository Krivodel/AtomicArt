using Microsoft.Extensions.Options;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;

using AtomicArt.Desktop.Resources;
using AtomicArt.Desktop.Services;
using AtomicArt.Desktop.Services.Generation;
using AtomicArt.Desktop.Services.Localization;
using AtomicArt.Desktop.Services.Updates;

namespace AtomicArt.Desktop.ViewModels.Updates;

public sealed partial class ApplicationUpdateViewModel :
    ObservableObject,
    IRecipient<LocalizationChangedMessage>,
    IDisposable
{
    public bool HasErrorMessage => !string.IsNullOrWhiteSpace(ErrorMessage);
    public bool IsDownloading => State == ApplicationUpdateState.Downloading;
    public bool IsWaitingForGeneration => State == ApplicationUpdateState.WaitingForGeneration;
    public bool IsProgressVisible => State is ApplicationUpdateState.WaitingForGeneration
        or ApplicationUpdateState.Downloading
        or ApplicationUpdateState.Restarting;
    public bool IsActionVisible => State == ApplicationUpdateState.Available;
    public string UpdateActionText => IsGenerationActive
        ? _textProvider.Get(UpdateLocalizationKeys.Actions.WaitAndInstall)
        : _textProvider.Get(UpdateLocalizationKeys.Actions.Install);
    public int LocalizationRevision => _localizationRevision;

    private readonly IApplicationUpdateService _updateService;
    private readonly IApplicationUpdateRestartCoordinator _restartCoordinator;
    private readonly IGenerationActivityTracker _generationActivityTracker;
    private readonly IUiThreadDispatcher _uiThreadDispatcher;
    private readonly IViewModelErrorHandler _errorHandler;
    private readonly ILocalizationTextProvider _textProvider;
    private readonly CancellationTokenSource _disposeCancellationSource = new();
    private readonly TimeSpan _updateCheckInterval;
    private ApplicationUpdate? _availableUpdate;
    private Task? _monitoringTask;
    private string? _dismissedVersion;
    private string? _messageLocalizationKey;
    private string? _messageArgument;
    private string? _errorLocalizationKey;
    private int _localizationRevision;
    private bool _isDisposed;
    private bool _isMonitoringStarted;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasErrorMessage))]
    private string? _errorMessage;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(UpdateActionText))]
    private bool _isGenerationActive;
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartMonitoringCommand))]
    [NotifyCanExecuteChangedFor(nameof(UpdateCommand))]
    [NotifyCanExecuteChangedFor(nameof(UpdateLaterCommand))]
    private bool _isLoading;
    [ObservableProperty]
    private bool _isNotificationOpen;
    [ObservableProperty]
    private int _downloadProgress;
    [ObservableProperty]
    private string _message = string.Empty;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDownloading))]
    [NotifyPropertyChangedFor(nameof(IsWaitingForGeneration))]
    [NotifyPropertyChangedFor(nameof(IsProgressVisible))]
    [NotifyPropertyChangedFor(nameof(IsActionVisible))]
    [NotifyCanExecuteChangedFor(nameof(UpdateCommand))]
    [NotifyCanExecuteChangedFor(nameof(UpdateLaterCommand))]
    private ApplicationUpdateState _state;

    public ApplicationUpdateViewModel(
        IApplicationUpdateService updateService,
        IApplicationUpdateRestartCoordinator restartCoordinator,
        IGenerationActivityTracker generationActivityTracker,
        IUiThreadDispatcher uiThreadDispatcher,
        IMessenger messenger,
        IViewModelErrorHandler errorHandler,
        ILocalizationTextProvider textProvider,
        IOptions<ApplicationUpdateOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _updateService = updateService
            ?? throw new ArgumentNullException(nameof(updateService));
        _restartCoordinator = restartCoordinator
            ?? throw new ArgumentNullException(nameof(restartCoordinator));
        _generationActivityTracker = generationActivityTracker
            ?? throw new ArgumentNullException(nameof(generationActivityTracker));
        _uiThreadDispatcher = uiThreadDispatcher
            ?? throw new ArgumentNullException(nameof(uiThreadDispatcher));
        ArgumentNullException.ThrowIfNull(messenger);
        _errorHandler = errorHandler
            ?? throw new ArgumentNullException(nameof(errorHandler));
        _textProvider = textProvider
            ?? throw new ArgumentNullException(nameof(textProvider));
        _updateCheckInterval = TimeSpan.FromMinutes(
            options.Value.CheckIntervalMinutes);
        IsGenerationActive = _generationActivityTracker.IsActive;
        _generationActivityTracker.ActivityChanged += OnGenerationActivityChanged;
        messenger.Register<LocalizationChangedMessage>(this);
    }

    public void Receive(LocalizationChangedMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

        _localizationRevision++;
        OnPropertyChanged(nameof(LocalizationRevision));
        OnPropertyChanged(nameof(UpdateActionText));

        if (_messageLocalizationKey is not null)
        {
            Message = _messageArgument is null
                ? _textProvider.Get(_messageLocalizationKey)
                : _textProvider.Format(
                    _messageLocalizationKey,
                    _messageArgument);
        }

        if (_errorLocalizationKey is not null)
        {
            ErrorMessage = _textProvider.Get(_errorLocalizationKey);
        }
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        _generationActivityTracker.ActivityChanged -= OnGenerationActivityChanged;
        _disposeCancellationSource.Cancel();
        _disposeCancellationSource.Dispose();
    }

    [RelayCommand(CanExecute = nameof(CanStartMonitoring))]
    private async Task StartMonitoringAsync(CancellationToken ct)
    {
        _isMonitoringStarted = true;
        StartMonitoringCommand.NotifyCanExecuteChanged();

        try
        {
            IsLoading = true;
            ClearErrorMessage();
            await CheckForUpdateAsync(ct);
            _monitoringTask = MonitorForUpdatesAsync(_disposeCancellationSource.Token);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            ClearErrorMessage();
        }
        catch (Exception ex)
        {
            _errorHandler.Log(ex, nameof(StartMonitoringAsync));
            SetLocalizedErrorMessage(UpdateLocalizationKeys.Errors.CheckFailed);
            _monitoringTask = MonitorForUpdatesAsync(_disposeCancellationSource.Token);
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanUpdate))]
    private async Task UpdateAsync(CancellationToken ct)
    {
        ApplicationUpdate? update = _availableUpdate;

        if (update is null)
        {
            return;
        }

        try
        {
            IsLoading = true;
            ClearErrorMessage();
            await WaitForGenerationIfRequiredAsync(ct);

            State = ApplicationUpdateState.Downloading;
            SetLocalizedMessage(UpdateLocalizationKeys.States.Downloading);
            DownloadProgress = 0;
            Progress<int> progress = new(value => DownloadProgress = value);
            await _updateService.DownloadUpdateAsync(update, progress, ct);

            await WaitForGenerationIfRequiredAsync(ct);
            State = ApplicationUpdateState.Restarting;
            SetLocalizedMessage(UpdateLocalizationKeys.States.Restarting);
            await _restartCoordinator.ApplyAndRestartAsync(update, ct);
            IsNotificationOpen = false;
            State = ApplicationUpdateState.Hidden;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            RestoreAvailableState(update);
        }
        catch (Exception ex)
        {
            _errorHandler.Log(ex, nameof(UpdateAsync));
            SetLocalizedErrorMessage(UpdateLocalizationKeys.Errors.InstallFailed);
            RestoreAvailableState(update);
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanUpdate))]
    private void UpdateLater()
    {
        if (_availableUpdate is not { } update)
        {
            return;
        }

        _dismissedVersion = update.Version;
        IsNotificationOpen = false;
        State = ApplicationUpdateState.Hidden;
    }

    private bool CanStartMonitoring()
    {
        return !_isMonitoringStarted && !IsLoading;
    }

    private bool CanUpdate()
    {
        return (State == ApplicationUpdateState.Available)
            && !IsLoading
            && (_availableUpdate is not null);
    }

    private async Task CheckForUpdateAsync(CancellationToken ct)
    {
        if (!_updateService.CanCheckForUpdates)
        {
            return;
        }

        ApplicationUpdate? update = await _updateService.CheckForUpdateAsync(ct);

        if (update is null
            || string.Equals(update.Version, _dismissedVersion, StringComparison.Ordinal))
        {
            return;
        }

        _availableUpdate = update;
        IsGenerationActive = _generationActivityTracker.IsActive;
        ShowAvailableUpdate(update);
    }

    private async Task MonitorForUpdatesAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_updateCheckInterval, ct);
                await CheckForUpdateAsync(ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _errorHandler.Log(ex, nameof(MonitorForUpdatesAsync));
            }
        }
    }

    private async Task WaitForGenerationIfRequiredAsync(CancellationToken ct)
    {
        if (!_generationActivityTracker.IsActive)
        {
            return;
        }

        State = ApplicationUpdateState.WaitingForGeneration;
        SetLocalizedMessage(UpdateLocalizationKeys.States.WaitingForGeneration);
        await _generationActivityTracker.WaitUntilIdleAsync(ct);
    }

    private void RestoreAvailableState(ApplicationUpdate update)
    {
        ShowAvailableUpdate(update);
    }

    private void ShowAvailableUpdate(ApplicationUpdate update)
    {
        SetLocalizedMessage(
            UpdateLocalizationKeys.AvailableFormat,
            update.Version);
        State = ApplicationUpdateState.Available;
        IsNotificationOpen = true;
    }

    private async Task RefreshGenerationActivityAsync()
    {
        await ViewModelUiDispatch.RunAsync(
            _uiThreadDispatcher,
            () => IsGenerationActive = _generationActivityTracker.IsActive,
            _disposeCancellationSource.Token,
            _errorHandler,
            nameof(RefreshGenerationActivityAsync));
    }

    private void ClearErrorMessage()
    {
        _errorLocalizationKey = null;
        ErrorMessage = null;
    }

    private void SetLocalizedErrorMessage(string localizationKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(localizationKey);

        _errorLocalizationKey = localizationKey;
        ErrorMessage = _textProvider.Get(localizationKey);
    }

    private void SetLocalizedMessage(
        string localizationKey,
        string? argument = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(localizationKey);

        _messageLocalizationKey = localizationKey;
        _messageArgument = argument;
        Message = argument is null
            ? _textProvider.Get(localizationKey)
            : _textProvider.Format(localizationKey, argument);
    }

    private void OnGenerationActivityChanged(object? sender, EventArgs e)
    {
        _ = sender;
        _ = e;
        _ = RefreshGenerationActivityAsync();
    }
}
