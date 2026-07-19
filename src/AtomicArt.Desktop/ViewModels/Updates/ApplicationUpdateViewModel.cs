using System.Globalization;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using AtomicArt.Desktop.Resources;
using AtomicArt.Desktop.Services;
using AtomicArt.Desktop.Services.Generation;
using AtomicArt.Desktop.Services.Updates;

namespace AtomicArt.Desktop.ViewModels.Updates;

public sealed partial class ApplicationUpdateViewModel : ObservableObject, IDisposable
{
    public bool HasErrorMessage => !string.IsNullOrWhiteSpace(ErrorMessage);
    public bool IsDownloading => State == ApplicationUpdateState.Downloading;
    public bool IsWaitingForGeneration => State == ApplicationUpdateState.WaitingForGeneration;
    public bool IsProgressVisible => State is ApplicationUpdateState.WaitingForGeneration
        or ApplicationUpdateState.Downloading
        or ApplicationUpdateState.Restarting;
    public bool IsActionVisible => State == ApplicationUpdateState.Available;
    public string UpdateActionText => IsGenerationActive
        ? UiStrings.UpdateWaitAndInstall
        : UiStrings.UpdateInstall;

    private static readonly TimeSpan UpdateCheckInterval = TimeSpan.FromMinutes(30);

    private readonly IApplicationUpdateService _updateService;
    private readonly IApplicationUpdateRestartCoordinator _restartCoordinator;
    private readonly IGenerationActivityTracker _generationActivityTracker;
    private readonly IUiThreadDispatcher _uiThreadDispatcher;
    private readonly IViewModelErrorHandler _errorHandler;
    private readonly CancellationTokenSource _disposeCancellationSource = new();
    private ApplicationUpdate? _availableUpdate;
    private Task? _monitoringTask;
    private string? _dismissedVersion;
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
        IViewModelErrorHandler errorHandler)
    {
        _updateService = updateService
            ?? throw new ArgumentNullException(nameof(updateService));
        _restartCoordinator = restartCoordinator
            ?? throw new ArgumentNullException(nameof(restartCoordinator));
        _generationActivityTracker = generationActivityTracker
            ?? throw new ArgumentNullException(nameof(generationActivityTracker));
        _uiThreadDispatcher = uiThreadDispatcher
            ?? throw new ArgumentNullException(nameof(uiThreadDispatcher));
        _errorHandler = errorHandler
            ?? throw new ArgumentNullException(nameof(errorHandler));
        IsGenerationActive = _generationActivityTracker.IsActive;
        _generationActivityTracker.ActivityChanged += OnGenerationActivityChanged;
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
            ErrorMessage = null;
            await CheckForUpdateAsync(ct);
            _monitoringTask = MonitorForUpdatesAsync(_disposeCancellationSource.Token);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            ErrorMessage = null;
        }
        catch (Exception ex)
        {
            _errorHandler.Log(ex, nameof(StartMonitoringAsync));
            ErrorMessage = UiStrings.UpdateCheckFailed;
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
            ErrorMessage = null;
            await WaitForGenerationIfRequiredAsync(ct);

            State = ApplicationUpdateState.Downloading;
            Message = UiStrings.UpdateDownloading;
            DownloadProgress = 0;
            Progress<int> progress = new(value => DownloadProgress = value);
            await _updateService.DownloadUpdateAsync(update, progress, ct);

            await WaitForGenerationIfRequiredAsync(ct);
            State = ApplicationUpdateState.Restarting;
            Message = UiStrings.UpdateRestarting;
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
            ErrorMessage = UiStrings.UpdateInstallFailed;
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
                await Task.Delay(UpdateCheckInterval, ct);
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
        Message = UiStrings.UpdateWaitingForGeneration;
        await _generationActivityTracker.WaitUntilIdleAsync(ct);
    }

    private void RestoreAvailableState(ApplicationUpdate update)
    {
        ShowAvailableUpdate(update);
    }

    private void ShowAvailableUpdate(ApplicationUpdate update)
    {
        Message = string.Format(
            CultureInfo.CurrentCulture,
            UiStrings.UpdateAvailableFormat,
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

    private void OnGenerationActivityChanged(object? sender, EventArgs e)
    {
        _ = sender;
        _ = e;
        _ = RefreshGenerationActivityAsync();
    }
}
