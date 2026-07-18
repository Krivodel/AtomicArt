using Microsoft.Extensions.Logging;

using AtomicArt.Desktop.Services.Generation;

namespace AtomicArt.Desktop.Services.Gallery;

public sealed class GalleryLifecycleController : IDisposable
{
    private static readonly TimeSpan ElapsedRefreshInterval = TimeSpan.FromSeconds(1);

    private readonly IGalleryLifecycleViewState _viewState;
    private readonly IViewModelErrorHandler _errorHandler;
    private readonly IGenerationActivityTracker _generationActivityTracker;
    private readonly ILogger<GalleryLifecycleController> _logger;
    private readonly IReadOnlyDictionary<GenerationLifecycleStatus, IGalleryLifecycleEventHandler> _handlersByStatus;
    private readonly IDisposable _lifecycleSubscription;
    private readonly CancellationTokenSource _disposeCancellation = new();
    private readonly Task _elapsedRefreshTask;

    public GalleryLifecycleController(
        IGenerationLifecycleEventHub lifecycleEventHub,
        IGalleryLifecycleViewState viewState,
        IViewModelErrorHandler errorHandler,
        IGenerationActivityTracker generationActivityTracker,
        IEnumerable<IGalleryLifecycleEventHandler> lifecycleEventHandlers,
        ILogger<GalleryLifecycleController> logger)
    {
        ArgumentNullException.ThrowIfNull(lifecycleEventHub);
        ArgumentNullException.ThrowIfNull(viewState);
        ArgumentNullException.ThrowIfNull(errorHandler);
        ArgumentNullException.ThrowIfNull(generationActivityTracker);
        ArgumentNullException.ThrowIfNull(lifecycleEventHandlers);
        ArgumentNullException.ThrowIfNull(logger);

        _viewState = viewState;
        _errorHandler = errorHandler;
        _generationActivityTracker = generationActivityTracker;
        _logger = logger;
        _handlersByStatus = lifecycleEventHandlers.ToDictionary(handler => handler.Status);
        _lifecycleSubscription = lifecycleEventHub.Subscribe(OnGenerationLifecycleEvent);
        _elapsedRefreshTask = ObserveLifecycleTaskAsync(
            RefreshElapsedTextLoopAsync(_disposeCancellation.Token),
            nameof(RefreshElapsedTextLoopAsync));
        _logger.LogInformation(
            "Gallery lifecycle controller started with {HandlerCount} event handlers",
            _handlersByStatus.Count);
    }

    public void Dispose()
    {
        _logger.LogInformation("Gallery lifecycle controller is stopping");
        _disposeCancellation.Cancel();
        _lifecycleSubscription.Dispose();
        _disposeCancellation.Dispose();
    }

    private static bool IsTerminalStatus(GenerationLifecycleStatus status)
    {
        return status is GenerationLifecycleStatus.StartFailed
            or GenerationLifecycleStatus.Completed
            or GenerationLifecycleStatus.Failed;
    }

    private async Task RefreshElapsedTextLoopAsync(CancellationToken ct)
    {
        try
        {
            await RefreshElapsedTextAsync(ct);

            while (!ct.IsCancellationRequested)
            {
                await Task.Delay(ElapsedRefreshInterval, ct);
                await RefreshElapsedTextAsync(ct);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
        }
    }

    private Task RefreshElapsedTextAsync(CancellationToken ct)
    {
        DateTime utcNow = DateTime.UtcNow;

        return _viewState.RefreshElapsedTextAsync(utcNow, ct);
    }

    private void ObserveLifecycleTask(Task task, string operationName)
    {
        _ = ObserveLifecycleTaskAsync(task, operationName);
    }

    private void ObserveTerminalLifecycleTask(
        Task task,
        string operationName,
        Guid correlationId)
    {
        _generationActivityTracker.Start(
            correlationId,
            GenerationActivityPhase.ResultPersistence);
        _ = ObserveTerminalLifecycleTaskAsync(task, operationName, correlationId);
    }

    private async Task ObserveLifecycleTaskAsync(Task task, string operationName)
    {
        try
        {
            await task;
        }
        catch (OperationCanceledException) when (_disposeCancellation.IsCancellationRequested)
        {
        }
        catch (InvalidOperationException ex)
        {
            _errorHandler.Log(ex, operationName);
        }
        catch (IOException ex)
        {
            _errorHandler.Log(ex, operationName);
        }
        catch (UnauthorizedAccessException ex)
        {
            _errorHandler.Log(ex, operationName);
        }
        catch (Exception ex)
        {
            _errorHandler.Log(ex, operationName);
        }
    }

    private async Task ObserveTerminalLifecycleTaskAsync(
        Task task,
        string operationName,
        Guid correlationId)
    {
        try
        {
            await ObserveLifecycleTaskAsync(task, operationName);
        }
        finally
        {
            _generationActivityTracker.Complete(
                correlationId,
                GenerationActivityPhase.ResultPersistence);
        }
    }

    private void OnGenerationLifecycleEvent(GenerationLifecycleEvent lifecycleEvent)
    {
        if (lifecycleEvent.Status == GenerationLifecycleStatus.StartRequested)
        {
            return;
        }

        if (!_handlersByStatus.TryGetValue(lifecycleEvent.Status, out IGalleryLifecycleEventHandler? handler))
        {
            _logger.LogWarning(
                "Gallery ignored lifecycle status {Status} for correlation {CorrelationId} because no handler is registered",
                lifecycleEvent.Status,
                lifecycleEvent.CorrelationId);

            return;
        }

        _logger.LogInformation(
            "Gallery is handling lifecycle status {Status} for correlation {CorrelationId}",
            lifecycleEvent.Status,
            lifecycleEvent.CorrelationId);

        if (IsTerminalStatus(lifecycleEvent.Status))
        {
            ObserveTerminalLifecycleTask(
                handler.HandleAsync(lifecycleEvent, _disposeCancellation.Token),
                handler.GetType().Name,
                lifecycleEvent.CorrelationId);
        }
        else
        {
            ObserveLifecycleTask(
                handler.HandleAsync(lifecycleEvent, _disposeCancellation.Token),
                handler.GetType().Name);
        }
    }
}
