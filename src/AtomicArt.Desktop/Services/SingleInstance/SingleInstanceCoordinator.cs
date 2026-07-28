using Microsoft.Extensions.Logging;

namespace AtomicArt.Desktop.Services.SingleInstance;

internal sealed class SingleInstanceCoordinator : IDisposable
{
    private readonly SingleInstanceIdentity _identity;
    private readonly SingleInstanceActivationChannel _activationChannel;
    private readonly ILogger<SingleInstanceCoordinator> _logger;
    private readonly object _activationLock = new();

    private FileStream? _lockStream;
    private CancellationTokenSource? _listenerCancellation;
    private Task? _listenerTask;
    private Func<Task>? _activationHandler;
    private TaskCompletionSource? _pendingActivation;
    private bool _isStarted;
    private bool _isDisposed;

    public SingleInstanceCoordinator(
        SingleInstanceIdentity identity,
        ILogger<SingleInstanceCoordinator> logger,
        SingleInstanceOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _identity = identity
            ?? throw new ArgumentNullException(nameof(identity));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _activationChannel = new SingleInstanceActivationChannel(
            identity.PipeName,
            logger,
            options);
    }

    public bool TryStartOrNotifyExisting()
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);

        if (_isStarted)
        {
            throw new InvalidOperationException(
                "Single-instance coordination has already started.");
        }

        _isStarted = true;
        EnsureCoordinationDirectoryExists();

        if (TryAcquireLock())
        {
            StartListener();
            return true;
        }

        if (_activationChannel.TryNotifyExisting())
        {
            return false;
        }

        if (TryAcquireLock())
        {
            StartListener();
            return true;
        }

        _logger.LogWarning(
            "Another Atomic Art process owns the instance lock but did not accept the activation request.");

        return false;
    }

    public void AttachActivationHandler(Func<Task> activationHandler)
    {
        ArgumentNullException.ThrowIfNull(activationHandler);
        ObjectDisposedException.ThrowIf(_isDisposed, this);
        TaskCompletionSource? pendingActivation;

        lock (_activationLock)
        {
            _activationHandler = activationHandler;
            pendingActivation = _pendingActivation;
            _pendingActivation = null;
        }

        if (pendingActivation is not null)
        {
            _ = CompletePendingActivationAsync(
                activationHandler,
                pendingActivation);
        }
    }

    public void DetachActivationHandler()
    {
        lock (_activationLock)
        {
            _activationHandler = null;
        }
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        TaskCompletionSource? pendingActivation;

        lock (_activationLock)
        {
            _activationHandler = null;
            pendingActivation = _pendingActivation;
            _pendingActivation = null;
        }

        pendingActivation?.TrySetCanceled();
        StopListener();
        _lockStream?.Dispose();
        _lockStream = null;
    }

    private void EnsureCoordinationDirectoryExists()
    {
        string? coordinationDirectory = Path.GetDirectoryName(
            _identity.LockFilePath);

        if (string.IsNullOrWhiteSpace(coordinationDirectory))
        {
            throw new InvalidOperationException(
                "The single-instance coordination directory could not be determined.");
        }

        Directory.CreateDirectory(coordinationDirectory);
    }

    private bool TryAcquireLock()
    {
        try
        {
            _lockStream = new FileStream(
                _identity.LockFilePath,
                FileMode.OpenOrCreate,
                FileAccess.ReadWrite,
                FileShare.None);

            return true;
        }
        catch (IOException)
        {
            return false;
        }
    }

    private void StartListener()
    {
        _listenerCancellation = new CancellationTokenSource();
        _listenerTask = Task.Run(
            () => _activationChannel.ListenAsync(
                RequestActivationAsync,
                _listenerCancellation.Token));
    }

    private void StopListener()
    {
        _listenerCancellation?.Cancel();

        try
        {
            _listenerTask?.GetAwaiter().GetResult();
        }
        catch (OperationCanceledException)
        {
        }

        _listenerCancellation?.Dispose();
        _listenerCancellation = null;
        _listenerTask = null;
    }

    private Task RequestActivationAsync()
    {
        Func<Task>? activationHandler;

        lock (_activationLock)
        {
            activationHandler = _activationHandler;

            if (activationHandler is null)
            {
                _pendingActivation ??= new TaskCompletionSource(
                    TaskCreationOptions.RunContinuationsAsynchronously);

                return _pendingActivation.Task;
            }
        }

        return RunActivationHandlerAsync(activationHandler);
    }

    private async Task RunActivationHandlerAsync(
        Func<Task> activationHandler)
    {
        try
        {
            await activationHandler().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Atomic Art failed to activate the existing main window.");
        }
    }

    private async Task CompletePendingActivationAsync(
        Func<Task> activationHandler,
        TaskCompletionSource pendingActivation)
    {
        await RunActivationHandlerAsync(activationHandler)
            .ConfigureAwait(false);
        pendingActivation.TrySetResult();
    }
}
