namespace AtomicArt.Desktop.Services.Generation;

public sealed class GenerationAdmissionGate : IGenerationAdmissionGate
{
    private readonly object _syncRoot = new();
    private TaskCompletionSource _activeRunsDrainedSource = CreateCompletedSource();
    private TaskCompletionSource _pauseCompletedSource = CreateCompletedSource();
    private int _activeRunCount;
    private bool _isPaused;

    public async Task<GenerationAdmissionLease> EnterAsync(CancellationToken ct)
    {
        while (true)
        {
            Task pauseCompletedTask;

            lock (_syncRoot)
            {
                if (!_isPaused)
                {
                    if (_activeRunCount == 0)
                    {
                        _activeRunsDrainedSource = CreatePendingSource();
                    }

                    _activeRunCount++;

                    return new GenerationAdmissionLease(ReleaseRun);
                }

                pauseCompletedTask = _pauseCompletedSource.Task;
            }

            await pauseCompletedTask.WaitAsync(ct).ConfigureAwait(false);
        }
    }

    public async Task<GenerationAdmissionPause> PauseAsync(CancellationToken ct)
    {
        Task activeRunsDrainedTask;

        lock (_syncRoot)
        {
            if (_isPaused)
            {
                throw new InvalidOperationException("Generation admission is already paused.");
            }

            _isPaused = true;
            _pauseCompletedSource = CreatePendingSource();
            activeRunsDrainedTask = _activeRunsDrainedSource.Task;
        }

        try
        {
            await activeRunsDrainedTask.WaitAsync(ct).ConfigureAwait(false);

            return new GenerationAdmissionPause(ReleasePause);
        }
        catch
        {
            ReleasePause();
            throw;
        }
    }

    private static TaskCompletionSource CreateCompletedSource()
    {
        TaskCompletionSource source = CreatePendingSource();
        source.SetResult();

        return source;
    }

    private static TaskCompletionSource CreatePendingSource()
    {
        return new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private void ReleaseRun()
    {
        TaskCompletionSource? drainedSource = null;

        lock (_syncRoot)
        {
            if (_activeRunCount <= 0)
            {
                throw new InvalidOperationException("Generation admission lease count is invalid.");
            }

            _activeRunCount--;

            if (_activeRunCount == 0)
            {
                drainedSource = _activeRunsDrainedSource;
            }
        }

        drainedSource?.TrySetResult();
    }

    private void ReleasePause()
    {
        TaskCompletionSource? completedSource = null;

        lock (_syncRoot)
        {
            if (!_isPaused)
            {
                return;
            }

            _isPaused = false;
            completedSource = _pauseCompletedSource;
        }

        completedSource.TrySetResult();
    }
}
