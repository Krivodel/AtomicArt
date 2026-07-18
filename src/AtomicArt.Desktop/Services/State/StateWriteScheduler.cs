using Microsoft.Extensions.Logging;

namespace AtomicArt.Desktop.Services.State;

public sealed class StateWriteScheduler : IStateWriteScheduler
{
    private readonly IAppStateStore _stateStore;
    private readonly ILogger<StateWriteScheduler> _logger;
    private readonly object _syncRoot;
    private readonly Dictionary<string, PendingWrite> _pendingWrites;
    private readonly HashSet<Task> _runningWrites;
    private readonly TimeSpan _writeDelay;

    public StateWriteScheduler(
        IAppStateStore stateStore,
        ILogger<StateWriteScheduler> logger)
        : this(stateStore, logger, StateWritePolicy.DeferredWriteDelay)
    {
    }

    internal StateWriteScheduler(
        IAppStateStore stateStore,
        ILogger<StateWriteScheduler> logger,
        TimeSpan writeDelay)
    {
        _stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _syncRoot = new object();
        _pendingWrites = new Dictionary<string, PendingWrite>(StringComparer.Ordinal);
        _runningWrites = [];
        _writeDelay = writeDelay;
    }

    public void ScheduleWrite<TState>(
        IStateSection section,
        TState state,
        StateWriteMode mode)
        where TState : notnull
    {
        ArgumentNullException.ThrowIfNull(section);
        ArgumentNullException.ThrowIfNull(state);

        if (mode == StateWriteMode.Immediate)
        {
            ScheduleImmediateWrite(section, state);
            return;
        }

        if (mode != StateWriteMode.Deferred)
        {
            throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unknown state write mode.");
        }

        CancellationTokenSource delayCancellation = new();
        PendingWrite pendingWrite = new(section, state, delayCancellation, delayCancellation.Token);
        CancellationTokenSource? previousCancellation = null;

        lock (_syncRoot)
        {
            if (_pendingWrites.TryGetValue(section.Key, out PendingWrite? previousWrite))
            {
                previousCancellation = previousWrite.DelayCancellation;
            }

            _pendingWrites[section.Key] = pendingWrite;
        }

        previousCancellation?.Cancel();
        _ = ProcessDelayedWriteAsync(section.Key, pendingWrite);
    }

    public async Task FlushAsync(CancellationToken ct)
    {
        List<PendingWrite> writes = [];

        lock (_syncRoot)
        {
            writes.AddRange(_pendingWrites.Values);
            _pendingWrites.Clear();
        }

        List<Exception> failures = [];

        foreach (PendingWrite write in writes)
        {
            write.DelayCancellation.Cancel();

            try
            {
                await SaveAsync(write, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to flush state section {SectionKey}.",
                    write.Section.Key);
                failures.Add(ex);
            }
            finally
            {
                write.Dispose();
            }
        }

        if (failures.Count > 0)
        {
            throw new AggregateException("One or more pending state writes failed.", failures);
        }

        await WaitForRunningWritesAsync(ct).ConfigureAwait(false);
    }

    private async Task ProcessDelayedWriteAsync(string sectionKey, PendingWrite pendingWrite)
    {
        CancellationToken delayToken = pendingWrite.DelayToken;

        try
        {
            await Task.Delay(_writeDelay, delayToken).ConfigureAwait(false);

            if (!TryRemoveCurrent(sectionKey, pendingWrite))
            {
                return;
            }

            TrackWrite(pendingWrite, CancellationToken.None);
        }
        catch (OperationCanceledException) when (delayToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to save scheduled state section {SectionKey}.",
                pendingWrite.Section.Key);
        }
        finally
        {
            pendingWrite.Dispose();
        }
    }

    private bool TryRemoveCurrent(string sectionKey, PendingWrite pendingWrite)
    {
        lock (_syncRoot)
        {
            if (_pendingWrites.TryGetValue(sectionKey, out PendingWrite? currentWrite)
                && ReferenceEquals(currentWrite, pendingWrite))
            {
                _pendingWrites.Remove(sectionKey);
                return true;
            }
        }

        return false;
    }

    private Task SaveAsync(PendingWrite pendingWrite, CancellationToken ct)
    {
        return _stateStore.SaveAsync(pendingWrite.Section, pendingWrite.State, ct);
    }

    private void ScheduleImmediateWrite(IStateSection section, object state)
    {
        CancelPendingWrite(section.Key);

        CancellationTokenSource delayCancellation = new();
        PendingWrite pendingWrite = new(
            section,
            state,
            delayCancellation,
            delayCancellation.Token);

        TrackWrite(pendingWrite, CancellationToken.None);
    }

    private void CancelPendingWrite(string sectionKey)
    {
        CancellationTokenSource? delayCancellation = null;

        lock (_syncRoot)
        {
            if (_pendingWrites.Remove(sectionKey, out PendingWrite? pendingWrite))
            {
                delayCancellation = pendingWrite.DelayCancellation;
            }
        }

        delayCancellation?.Cancel();
    }

    private void TrackWrite(PendingWrite pendingWrite, CancellationToken ct)
    {
        Task writeTask = SaveTrackedAsync(pendingWrite, ct);

        lock (_syncRoot)
        {
            _runningWrites.Add(writeTask);
        }

        _ = writeTask.ContinueWith(
            RemoveRunningWrite,
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    private async Task SaveTrackedAsync(PendingWrite pendingWrite, CancellationToken ct)
    {
        try
        {
            await SaveAsync(pendingWrite, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to save state section {SectionKey}.",
                pendingWrite.Section.Key);
        }
        finally
        {
            pendingWrite.Dispose();
        }
    }

    private async Task WaitForRunningWritesAsync(CancellationToken ct)
    {
        while (true)
        {
            Task[] runningWrites;

            lock (_syncRoot)
            {
                if (_runningWrites.Count == 0)
                {
                    return;
                }

                runningWrites = _runningWrites.ToArray();
            }

            await Task.WhenAll(runningWrites).WaitAsync(ct).ConfigureAwait(false);
        }
    }

    private void RemoveRunningWrite(Task writeTask)
    {
        lock (_syncRoot)
        {
            _runningWrites.Remove(writeTask);
        }
    }

    private sealed class PendingWrite : IDisposable
    {
        private int _disposed;

        public IStateSection Section { get; }
        public object State { get; }
        public CancellationTokenSource DelayCancellation { get; }
        public CancellationToken DelayToken { get; }

        public PendingWrite(
            IStateSection section,
            object state,
            CancellationTokenSource delayCancellation,
            CancellationToken delayToken)
        {
            Section = section;
            State = state;
            DelayCancellation = delayCancellation;
            DelayToken = delayToken;
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                DelayCancellation.Dispose();
            }
        }
    }
}
