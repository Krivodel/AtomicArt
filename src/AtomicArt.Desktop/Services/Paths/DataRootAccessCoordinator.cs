namespace AtomicArt.Desktop.Services.Paths;

public sealed class DataRootAccessCoordinator : IDataRootAccessCoordinator
{
    private readonly object _syncRoot = new();
    private TaskCompletionSource _accessDrainedSource = CreateCompletedSource();
    private TaskCompletionSource _migrationCompletedSource = CreateCompletedSource();
    private int _activeAccessCount;
    private bool _isMigrationActive;

    public async Task<DataRootAccessLease> AcquireAccessAsync(CancellationToken ct)
    {
        while (true)
        {
            Task migrationCompletedTask;

            lock (_syncRoot)
            {
                if (!_isMigrationActive)
                {
                    if (_activeAccessCount == 0)
                    {
                        _accessDrainedSource = CreatePendingSource();
                    }

                    _activeAccessCount++;

                    return new DataRootAccessLease(ReleaseAccess);
                }

                migrationCompletedTask = _migrationCompletedSource.Task;
            }

            await migrationCompletedTask.WaitAsync(ct).ConfigureAwait(false);
        }
    }

    public async Task<DataRootMigrationLease> BeginMigrationAsync(CancellationToken ct)
    {
        Task accessDrainedTask;

        lock (_syncRoot)
        {
            if (_isMigrationActive)
            {
                throw new InvalidOperationException("A data root migration is already active.");
            }

            _isMigrationActive = true;
            _migrationCompletedSource = CreatePendingSource();
            accessDrainedTask = _accessDrainedSource.Task;
        }

        try
        {
            await accessDrainedTask.WaitAsync(ct).ConfigureAwait(false);

            return new DataRootMigrationLease(ReleaseMigration);
        }
        catch
        {
            ReleaseMigration();
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

    private void ReleaseAccess()
    {
        TaskCompletionSource? drainedSource = null;

        lock (_syncRoot)
        {
            if (_activeAccessCount <= 0)
            {
                throw new InvalidOperationException("Data root access lease count is invalid.");
            }

            _activeAccessCount--;

            if (_activeAccessCount == 0)
            {
                drainedSource = _accessDrainedSource;
            }
        }

        drainedSource?.TrySetResult();
    }

    private void ReleaseMigration()
    {
        TaskCompletionSource? completedSource = null;

        lock (_syncRoot)
        {
            if (!_isMigrationActive)
            {
                return;
            }

            _isMigrationActive = false;
            completedSource = _migrationCompletedSource;
        }

        completedSource.TrySetResult();
    }
}
