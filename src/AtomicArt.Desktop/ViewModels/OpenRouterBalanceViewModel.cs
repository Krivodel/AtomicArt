using System.Globalization;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;

using AtomicArt.Desktop.Resources;
using AtomicArt.Desktop.Services;
using AtomicArt.Desktop.Services.Localization;
using AtomicArt.Desktop.Services.OpenRouter;

namespace AtomicArt.Desktop.ViewModels;

public sealed partial class OpenRouterBalanceViewModel :
    ObservableObject,
    IRecipient<LocalizationChangedMessage>,
    IRecipient<SecretChangedMessage>,
    IDisposable
{
    public string BalanceText => Balance is decimal amount
        ? _textProvider.Format(ShellLocalizationKeys.OpenRouterBalanceFormat,
            amount.ToString("0.##", CultureInfo.InvariantCulture))
        : _textProvider.Get(ShellLocalizationKeys.OpenRouterBalanceUnavailable);
    public bool HasErrorMessage => !string.IsNullOrWhiteSpace(ErrorMessage);

    private static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(30);
    private readonly IOpenRouterBalanceApiClient _apiClient;
    private readonly ISecretStore _secretStore;
    private readonly IUiThreadDispatcher _uiThreadDispatcher;
    private readonly IViewModelErrorHandler _errorHandler;
    private readonly ILocalizationTextProvider _textProvider;
    private readonly TimeProvider _timeProvider;
    private readonly IMessenger _messenger;
    private readonly IDisposable _generationSubscription;
    private readonly CancellationTokenSource _disposeCancellationSource = new();
    private readonly CancellationToken _disposeToken;
    private Task? _refreshTask;
    private bool _refreshRequested;
    private bool _isMonitoringStarted;
    private bool _isWindowActive;
    private bool _isDisposed;
    private int _credentialRevision;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BalanceText))]
    private decimal? _balance;
    [ObservableProperty]
    private bool _hasManagementKey;
    [ObservableProperty]
    private bool _isLoading;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasErrorMessage))]
    private string? _errorMessage;

    public OpenRouterBalanceViewModel(
        IOpenRouterBalanceApiClient apiClient,
        ISecretStore secretStore,
        IGenerationLifecycleEventHub generationEvents,
        IUiThreadDispatcher uiThreadDispatcher,
        IViewModelErrorHandler errorHandler,
        ILocalizationTextProvider textProvider,
        TimeProvider timeProvider,
        IMessenger messenger)
    {
        ArgumentNullException.ThrowIfNull(generationEvents);

        _apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
        _secretStore = secretStore ?? throw new ArgumentNullException(nameof(secretStore));
        _uiThreadDispatcher = uiThreadDispatcher ?? throw new ArgumentNullException(nameof(uiThreadDispatcher));
        _errorHandler = errorHandler ?? throw new ArgumentNullException(nameof(errorHandler));
        _textProvider = textProvider ?? throw new ArgumentNullException(nameof(textProvider));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _messenger = messenger ?? throw new ArgumentNullException(nameof(messenger));
        _disposeToken = _disposeCancellationSource.Token;
        _generationSubscription = generationEvents.Subscribe(OnGenerationChanged);
        messenger.Register<LocalizationChangedMessage>(this);
        messenger.Register<SecretChangedMessage>(this);
    }

    public void SetWindowActive(bool isActive)
    {
        if (_isDisposed || (_isWindowActive == isActive))
        {
            return;
        }

        _isWindowActive = isActive;

        if (isActive && _isMonitoringStarted)
        {
            _ = DispatchRefreshAsync();
        }
    }

    public Task RefreshAsync(CancellationToken ct)
    {
        if (_isDisposed)
        {
            return Task.CompletedTask;
        }

        ct.ThrowIfCancellationRequested();
        _refreshRequested = true;

        if (_refreshTask is null || _refreshTask.IsCompleted)
        {
            _refreshTask = ProcessRefreshRequestsAsync(_disposeToken);
        }

        return _refreshTask.WaitAsync(ct);
    }

    public void Receive(LocalizationChangedMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

        OnPropertyChanged(nameof(BalanceText));
    }

    public void Receive(SecretChangedMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

        if (string.Equals(message.Key, OpenRouterManagementKeySettingDefinition.SecretNameValue,
            StringComparison.Ordinal))
        {
            _ = DispatchRefreshAsync(InvalidateCredential);
        }
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        _generationSubscription.Dispose();
        _messenger.UnregisterAll(this);
        _disposeCancellationSource.Cancel();
        _disposeCancellationSource.Dispose();
    }

    [RelayCommand(CanExecute = nameof(CanStartMonitoring))]
    private async Task StartMonitoringAsync(CancellationToken ct)
    {
        _isMonitoringStarted = true;
        StartMonitoringCommand.NotifyCanExecuteChanged();
        _ = MonitorAsync(_disposeToken);
        await RefreshAsync(ct);
    }

    private bool CanStartMonitoring()
    {
        return !_isDisposed && !_isMonitoringStarted;
    }

    private async Task MonitorAsync(CancellationToken ct)
    {
        try
        {
            using PeriodicTimer timer = new(RefreshInterval, _timeProvider);

            while (await timer.WaitForNextTickAsync(ct).ConfigureAwait(false))
            {
                await _uiThreadDispatcher.InvokeAsync(async () =>
                {
                    if (_isWindowActive)
                    {
                        await RefreshAsync(ct);
                    }
                }, ct).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            _errorHandler.Log(exception, nameof(MonitorAsync));
        }
    }

    private async Task ProcessRefreshRequestsAsync(CancellationToken ct)
    {
        IsLoading = true;

        try
        {
            while (_refreshRequested && !ct.IsCancellationRequested)
            {
                _refreshRequested = false;
                await RefreshBalanceAsync(ct);
            }
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task RefreshBalanceAsync(CancellationToken ct)
    {
        int credentialRevision = _credentialRevision;

        try
        {
            string? managementKey = await _secretStore.GetSecretAsync(
                OpenRouterManagementKeySettingDefinition.SecretNameValue, ct);

            if (_isDisposed || (credentialRevision != _credentialRevision))
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(managementKey))
            {
                HasManagementKey = false;
                Balance = null;
                ErrorMessage = null;
                return;
            }

            HasManagementKey = true;
            decimal amount = await _apiClient.GetBalanceAsync(managementKey, ct);

            if (!_isDisposed && (credentialRevision == _credentialRevision))
            {
                Balance = amount;
                ErrorMessage = null;
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            _errorHandler.Log(exception, nameof(RefreshBalanceAsync));

            if (!_isDisposed && (credentialRevision == _credentialRevision))
            {
                ErrorMessage = _errorHandler.GetUserMessage(exception);
            }
        }
    }

    private async Task DispatchRefreshAsync(Action? prepareRefresh = null)
    {
        if (_isDisposed)
        {
            return;
        }

        CancellationToken ct = _disposeToken;

        try
        {
            await _uiThreadDispatcher.InvokeAsync(async () =>
            {
                prepareRefresh?.Invoke();
                await RefreshAsync(ct);
            }, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            _errorHandler.Log(exception, nameof(DispatchRefreshAsync));
        }
    }

    private void InvalidateCredential()
    {
        _credentialRevision++;
        HasManagementKey = false;
        Balance = null;
        ErrorMessage = null;
    }

    private void OnGenerationChanged(GenerationLifecycleEvent lifecycleEvent)
    {
        if (lifecycleEvent.Status is GenerationLifecycleStatus.Completed
            or GenerationLifecycleStatus.Failed
            or GenerationLifecycleStatus.StartFailed)
        {
            _ = DispatchRefreshAsync();
        }
    }
}
