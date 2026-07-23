using Microsoft.Extensions.Logging;

using AtomicArt.Desktop.Services.Gallery;
using AtomicArt.Desktop.Services.UiAnimation;

namespace AtomicArt.Desktop.Controls.Gallery;

internal sealed class GalleryOperationQueueProcessor
{
    private readonly IUiFrameScheduler _frameScheduler;
    private readonly IGalleryOperationRunnerRegistry _runnerRegistry;
    private readonly GalleryOperationBatchDispatcher _batchDispatcher;
    private readonly ILogger<GalleryOperationQueueProcessor> _logger;
    private readonly object _syncRoot = new();
    private readonly Queue<GalleryOperation> _pendingOperations = [];
    private bool _flushScheduled;
    private bool _flushRunning;

    public GalleryOperationQueueProcessor(
        IUiFrameScheduler frameScheduler,
        IGalleryOperationRunnerRegistry runnerRegistry,
        GalleryOperationBatchDispatcher batchDispatcher,
        ILogger<GalleryOperationQueueProcessor> logger)
    {
        _frameScheduler = frameScheduler ?? throw new ArgumentNullException(nameof(frameScheduler));
        _runnerRegistry = runnerRegistry ?? throw new ArgumentNullException(nameof(runnerRegistry));
        _batchDispatcher = batchDispatcher ?? throw new ArgumentNullException(nameof(batchDispatcher));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    internal Task EnqueueAsync(
        GalleryOperation operation,
        GalleryOperationCoordinator context,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(context);
        ct.ThrowIfCancellationRequested();
        context.EnsureSceneAttached();

        lock (_syncRoot)
        {
            _pendingOperations.Enqueue(operation);
        }

        _logger.LogDebug(
            "Queued gallery operation {OperationType}",
            operation.GetType().Name);
        RequestRetarget(operation);
        ScheduleFlush(context, ct);

        return operation.Completion.Task;
    }

    internal List<GalleryOperation> DrainLeadingOperations(Type operationType)
    {
        ArgumentNullException.ThrowIfNull(operationType);

        List<GalleryOperation> operations = [];
        lock (_syncRoot)
        {
            while (_pendingOperations.TryPeek(out GalleryOperation? operation)
                   && GalleryOperationTypeSelector.Matches(operation, operationType))
            {
                operations.Add(_pendingOperations.Dequeue());
            }
        }

        return operations;
    }

    internal bool HasLeadingOperation(Type operationType)
    {
        ArgumentNullException.ThrowIfNull(operationType);

        lock (_syncRoot)
        {
            return _pendingOperations.TryPeek(out GalleryOperation? operation)
                   && GalleryOperationTypeSelector.Matches(operation, operationType);
        }
    }

    private void RequestRetarget(GalleryOperation operation)
    {
        IGalleryRetargetableOperationRunner? runner = _runnerRegistry
            .Runners
            .OfType<IGalleryRetargetableOperationRunner>()
            .FirstOrDefault(candidate => GalleryOperationTypeSelector.Matches(
                operation,
                candidate.OperationType));
        if (runner is { IsRunning: true })
        {
            runner.RequestRetarget();
        }
    }

    private void ScheduleFlush(GalleryOperationCoordinator context, CancellationToken ct)
    {
        lock (_syncRoot)
        {
            if (_flushScheduled || _flushRunning)
            {
                return;
            }

            _flushScheduled = true;
        }

        _frameScheduler.RequestAnimationFrame(frameTime =>
        {
            _ = frameTime;
            _ = FlushAsync(context, ct);
        });
    }

    private async Task FlushAsync(GalleryOperationCoordinator context, CancellationToken ct)
    {
        if (!TryBeginFlush(out List<GalleryOperation> operations))
        {
            return;
        }

        try
        {
            await FlushDrainedOperationsAsync(context, operations, ct);
        }
        finally
        {
            CompleteFlush(context, ct);
        }
    }

    private bool TryBeginFlush(out List<GalleryOperation> operations)
    {
        lock (_syncRoot)
        {
            if (_flushRunning)
            {
                operations = [];
                return false;
            }

            operations = DrainPendingOperationsCore();
            _flushScheduled = false;
            _flushRunning = true;

            return true;
        }
    }

    private void CompleteFlush(GalleryOperationCoordinator context, CancellationToken ct)
    {
        bool shouldSchedule;
        lock (_syncRoot)
        {
            _flushRunning = false;
            shouldSchedule = _pendingOperations.Count > 0;
        }

        if (shouldSchedule)
        {
            ScheduleFlush(context, ct);
        }
    }

    private async Task FlushDrainedOperationsAsync(
        GalleryOperationCoordinator context,
        IReadOnlyList<GalleryOperation> operations,
        CancellationToken ct)
    {
        try
        {
            ct.ThrowIfCancellationRequested();
            _logger.LogDebug(
                "Dispatching gallery operation batch with {OperationCount} operations",
                operations.Count);
            await _batchDispatcher.DispatchAsync(context, operations, ct);
            _logger.LogDebug(
                "Completed gallery operation batch with {OperationCount} operations",
                operations.Count);
        }
        catch (OperationCanceledException ex) when (ct.IsCancellationRequested)
        {
            _logger.LogDebug(
                ex,
                "Cancelled gallery operation batch with {OperationCount} operations",
                operations.Count);
            GalleryOperationCompletion.Cancel(operations, ct);
            CancelPendingOperations(ct);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to flush gallery operations.");
            GalleryOperationCompletion.Fail(operations, exception);
            FailPendingOperations(exception);
        }
    }

    private List<GalleryOperation> DrainPendingOperationsCore()
    {
        List<GalleryOperation> operations = [];
        while (_pendingOperations.TryDequeue(out GalleryOperation? operation))
        {
            operations.Add(operation);
        }

        return operations;
    }

    private void CancelPendingOperations(CancellationToken ct)
    {
        List<GalleryOperation> operations;
        lock (_syncRoot)
        {
            operations = DrainPendingOperationsCore();
        }

        GalleryOperationCompletion.Cancel(operations, ct);
    }

    private void FailPendingOperations(Exception exception)
    {
        List<GalleryOperation> operations;
        lock (_syncRoot)
        {
            operations = DrainPendingOperationsCore();
        }

        GalleryOperationCompletion.Fail(operations, exception);
    }
}
