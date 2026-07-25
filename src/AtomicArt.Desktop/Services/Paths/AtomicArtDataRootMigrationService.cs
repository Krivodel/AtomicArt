using Microsoft.Extensions.Logging;

using AtomicArt.Desktop.Services.Generation;
using AtomicArt.Desktop.Services.Gallery;
using AtomicArt.Desktop.Services.Logging;
using AtomicArt.Desktop.Services.State;

namespace AtomicArt.Desktop.Services.Paths;

internal sealed class AtomicArtDataRootMigrationService :
    IAtomicArtDataRootMigrationService
{
    private readonly IAtomicArtDataPathProvider _pathProvider;
    private readonly IAtomicArtDataPathSwitcher _pathSwitcher;
    private readonly AtomicArtDataRootBootstrapStore _bootstrapStore;
    private readonly DataRootMigrationJournalStore _journalStore;
    private readonly DataRootMigrationPlanner _planner;
    private readonly DataRootFileTransfer _fileTransfer;
    private readonly IGenerationAdmissionGate _generationAdmissionGate;
    private readonly IGenerationActivityTracker _generationActivityTracker;
    private readonly IDataRootAccessCoordinator _accessCoordinator;
    private readonly IApplicationStateFlushService _stateFlushService;
    private readonly IDataRootViewerPreparationService _viewerPreparationService;
    private readonly DataRootMigrationTargetAttachmentService _targetAttachmentService;
    private readonly IDataRootLogRelocationService _logRelocationService;
    private readonly IUiThreadDispatcher _uiThreadDispatcher;
    private readonly ILogger<AtomicArtDataRootMigrationService> _logger;
    private readonly SemaphoreSlim _migrationLock = new(1, 1);

    public AtomicArtDataRootMigrationService(
        IAtomicArtDataPathProvider pathProvider,
        IAtomicArtDataPathSwitcher pathSwitcher,
        AtomicArtDataRootBootstrapStore bootstrapStore,
        DataRootMigrationJournalStore journalStore,
        DataRootMigrationPlanner planner,
        DataRootFileTransfer fileTransfer,
        IGenerationAdmissionGate generationAdmissionGate,
        IGenerationActivityTracker generationActivityTracker,
        IDataRootAccessCoordinator accessCoordinator,
        IApplicationStateFlushService stateFlushService,
        IDataRootViewerPreparationService viewerPreparationService,
        DataRootMigrationTargetAttachmentService targetAttachmentService,
        IDataRootLogRelocationService logRelocationService,
        IUiThreadDispatcher uiThreadDispatcher,
        ILogger<AtomicArtDataRootMigrationService> logger)
    {
        _pathProvider = pathProvider ?? throw new ArgumentNullException(nameof(pathProvider));
        _pathSwitcher = pathSwitcher ?? throw new ArgumentNullException(nameof(pathSwitcher));
        _bootstrapStore = bootstrapStore ?? throw new ArgumentNullException(nameof(bootstrapStore));
        _journalStore = journalStore ?? throw new ArgumentNullException(nameof(journalStore));
        _planner = planner ?? throw new ArgumentNullException(nameof(planner));
        _fileTransfer = fileTransfer ?? throw new ArgumentNullException(nameof(fileTransfer));
        _generationAdmissionGate = generationAdmissionGate
            ?? throw new ArgumentNullException(nameof(generationAdmissionGate));
        _generationActivityTracker = generationActivityTracker
            ?? throw new ArgumentNullException(nameof(generationActivityTracker));
        _accessCoordinator = accessCoordinator
            ?? throw new ArgumentNullException(nameof(accessCoordinator));
        _stateFlushService = stateFlushService
            ?? throw new ArgumentNullException(nameof(stateFlushService));
        _viewerPreparationService = viewerPreparationService
            ?? throw new ArgumentNullException(nameof(viewerPreparationService));
        _targetAttachmentService = targetAttachmentService
            ?? throw new ArgumentNullException(nameof(targetAttachmentService));
        _logRelocationService = logRelocationService
            ?? throw new ArgumentNullException(nameof(logRelocationService));
        _uiThreadDispatcher = uiThreadDispatcher
            ?? throw new ArgumentNullException(nameof(uiThreadDispatcher));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task MigrateAsync(
        string destinationRootDirectory,
        IProgress<DataRootMigrationProgress> progress,
        CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationRootDirectory);
        ArgumentNullException.ThrowIfNull(progress);

        await _migrationLock.WaitAsync(ct).ConfigureAwait(false);

        try
        {
            await MigrateCoreAsync(destinationRootDirectory, progress, ct)
                .ConfigureAwait(false);
        }
        finally
        {
            _migrationLock.Release();
        }
    }

    private async Task MigrateCoreAsync(
        string destinationRootDirectory,
        IProgress<DataRootMigrationProgress> progress,
        CancellationToken ct)
    {
        Report(progress, DataRootMigrationProgressStage.Preparing, 0, 0, 0, 0);

        DataRootMigrationJournal? pendingJournal = _journalStore.Load();

        if (pendingJournal is not null)
        {
            IOException pendingRecoveryException = new(
                "A previous Atomic Art data root migration still requires recovery.");
            string activeRoot = Path.TrimEndingDirectorySeparator(
                Path.GetFullPath(_pathProvider.RootDirectory));
            string pendingDestination = Path.TrimEndingDirectorySeparator(
                Path.GetFullPath(pendingJournal.DestinationRootDirectory));

            if (string.Equals(
                    activeRoot,
                    pendingDestination,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new DataRootMigrationCleanupException(pendingRecoveryException);
            }

            throw new DataRootMigrationException(pendingRecoveryException);
        }

        string sourceRootDirectory = _pathProvider.RootDirectory;
        string destinationRoot = Path.TrimEndingDirectorySeparator(
            Path.GetFullPath(destinationRootDirectory));

        if (string.Equals(
                Path.TrimEndingDirectorySeparator(sourceRootDirectory),
                destinationRoot,
                StringComparison.OrdinalIgnoreCase))
        {
            Report(
                progress,
                DataRootMigrationProgressStage.Completed,
                0,
                0,
                0,
                0);
            return;
        }

        IDataRootMigrationTarget target = _targetAttachmentService.GetTarget();

        using GenerationAdmissionPause admissionPause =
            await _generationAdmissionGate.PauseAsync(ct).ConfigureAwait(false);
        await _generationActivityTracker.WaitUntilIdleAsync(ct).ConfigureAwait(false);
        await _uiThreadDispatcher.InvokeAsync(
            () => _viewerPreparationService.CloseAllAsync(ct),
            ct).ConfigureAwait(false);
        await _stateFlushService.FlushAsync(target, ct).ConfigureAwait(false);

        using DataRootMigrationLease migrationLease =
            await _accessCoordinator.BeginMigrationAsync(ct).ConfigureAwait(false);
        DataRootMigrationPlan? plan = null;
        bool rootSwitched = false;
        bool loggerPaused = false;
        IReadOnlyList<DataRootMigrationFile> verifiedFiles =
            Array.Empty<DataRootMigrationFile>();

        try
        {
            _logRelocationService.Pause();
            loggerPaused = true;
            plan = _planner.Create(
                sourceRootDirectory,
                destinationRoot,
                ct);
            verifiedFiles = plan.Files;
            DataRootMigrationJournal copyingJournal = CreateJournal(
                plan,
                DataRootMigrationStage.Copying,
                plan.Files);
            await _journalStore.SaveAsync(copyingJournal, ct).ConfigureAwait(false);
            verifiedFiles = await _fileTransfer
                .CopyAndVerifyAsync(plan, progress, ct)
                .ConfigureAwait(false);
            DataRootMigrationJournal readyJournal = CreateJournal(
                plan,
                DataRootMigrationStage.ReadyToSwitch,
                verifiedFiles);
            await _journalStore.SaveAsync(readyJournal, ct).ConfigureAwait(false);

            ReportSwitching(progress, plan);
            await _bootstrapStore
                .SaveRootDirectoryAsync(plan.DestinationRootDirectory, CancellationToken.None)
                .ConfigureAwait(false);
            _pathSwitcher.SwitchRootDirectory(plan.DestinationRootDirectory);
            rootSwitched = true;
            DataRootMigrationJournal switchedJournal = CreateJournal(
                plan,
                DataRootMigrationStage.Switched,
                verifiedFiles);
            await _journalStore
                .SaveAsync(switchedJournal, CancellationToken.None)
                .ConfigureAwait(false);
            migrationLease.Dispose();
            await _uiThreadDispatcher.InvokeAsync(
                () => target.RebaseDataRootAsync(
                    plan.SourceRootDirectory,
                    plan.DestinationRootDirectory,
                    CancellationToken.None),
                CancellationToken.None).ConfigureAwait(false);

            _logRelocationService.Resume(_pathProvider);
            loggerPaused = false;
            _logger.LogInformation(
                "Atomic Art data root switched successfully with {FileCount} files and {TotalBytes} bytes.",
                verifiedFiles.Count,
                plan.TotalBytes);
            await CleanupSourceAsync(plan, verifiedFiles, progress).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!rootSwitched && ct.IsCancellationRequested)
        {
            if (plan is not null)
            {
                CleanupDestinationAfterFailure(plan);
            }

            _journalStore.Delete();
            throw;
        }
        catch (Exception ex) when (!rootSwitched)
        {
            _logger.LogError(ex, "Atomic Art data root migration failed before switching roots.");

            if (plan is not null)
            {
                CleanupDestinationAfterFailure(plan);
            }

            _journalStore.Delete();
            throw new DataRootMigrationException(ex);
        }
        catch (DataRootMigrationCleanupException)
        {
            throw;
        }
        catch (Exception ex) when (rootSwitched)
        {
            _logger.LogWarning(
                ex,
                "Atomic Art switched data roots, but post-switch finalization is pending.");
            throw new DataRootMigrationCleanupException(ex);
        }
        finally
        {
            if (loggerPaused)
            {
                _logRelocationService.Resume(_pathProvider);
            }
        }
    }

    private static DataRootMigrationJournal CreateJournal(
        DataRootMigrationPlan plan,
        DataRootMigrationStage stage,
        IReadOnlyList<DataRootMigrationFile> files)
    {
        return new DataRootMigrationJournal
        {
            SourceRootDirectory = plan.SourceRootDirectory,
            DestinationRootDirectory = plan.DestinationRootDirectory,
            Stage = stage,
            Directories = plan.RelativeDirectories,
            Files = files
        };
    }

    private static void Report(
        IProgress<DataRootMigrationProgress> progress,
        DataRootMigrationProgressStage stage,
        long completedBytes,
        long totalBytes,
        int completedFiles,
        int totalFiles)
    {
        progress.Report(new DataRootMigrationProgress
        {
            Stage = stage,
            CompletedBytes = completedBytes,
            TotalBytes = totalBytes,
            CompletedFiles = completedFiles,
            TotalFiles = totalFiles
        });
    }

    private static void ReportSwitching(
        IProgress<DataRootMigrationProgress> progress,
        DataRootMigrationPlan plan)
    {
        long totalWorkBytes = checked(plan.TotalBytes * 3);
        Report(
            progress,
            DataRootMigrationProgressStage.Switching,
            totalWorkBytes,
            totalWorkBytes,
            plan.Files.Count,
            plan.Files.Count);
    }

    private async Task CleanupSourceAsync(
        DataRootMigrationPlan plan,
        IReadOnlyList<DataRootMigrationFile> verifiedFiles,
        IProgress<DataRootMigrationProgress> progress)
    {
        try
        {
            DataRootMigrationJournal cleanupJournal = CreateJournal(
                plan,
                DataRootMigrationStage.CleaningSource,
                verifiedFiles);
            await _journalStore
                .SaveAsync(cleanupJournal, CancellationToken.None)
                .ConfigureAwait(false);
            long totalWorkBytes = checked(plan.TotalBytes * 3);
            Report(
                progress,
                DataRootMigrationProgressStage.Cleaning,
                totalWorkBytes,
                totalWorkBytes,
                verifiedFiles.Count,
                verifiedFiles.Count);
            _fileTransfer.DeleteSourceFiles(
                plan.SourceRootDirectory,
                verifiedFiles,
                plan.RelativeDirectories);
            _journalStore.Delete();
            Report(
                progress,
                DataRootMigrationProgressStage.Completed,
                totalWorkBytes,
                totalWorkBytes,
                verifiedFiles.Count,
                verifiedFiles.Count);
        }
        catch (Exception ex) when (ex is IOException
            or UnauthorizedAccessException
            or NotSupportedException)
        {
            _logger.LogWarning(
                ex,
                "Atomic Art switched to the new data root, but cleanup of the previous root is pending.");
            throw new DataRootMigrationCleanupException(ex);
        }
    }

    private void CleanupDestinationAfterFailure(DataRootMigrationPlan plan)
    {
        try
        {
            _fileTransfer.DeleteCopiedFiles(
                plan.DestinationRootDirectory,
                plan.Files,
                plan.RelativeDirectories);
        }
        catch (Exception cleanupException) when (cleanupException is IOException
            or UnauthorizedAccessException
            or NotSupportedException)
        {
            _logger.LogWarning(
                cleanupException,
                "Failed to clean copied files after an interrupted data root migration.");
        }
    }
}
