using Microsoft.Extensions.Logging;

using Velopack;
using Velopack.Sources;

namespace AtomicArt.Desktop.Services.Updates;

public sealed class VelopackApplicationUpdateService : IApplicationUpdateService
{
    public bool CanCheckForUpdates => GetUpdateManager().IsInstalled;

    private const string RepositoryUrl = "https://github.com/Krivodel/AtomicArt";

    private readonly ILogger<VelopackApplicationUpdateService> _logger;
    private readonly object _syncRoot = new();
    private UpdateManager? _updateManager;

    public VelopackApplicationUpdateService(
        ILogger<VelopackApplicationUpdateService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<ApplicationUpdate?> CheckForUpdateAsync(CancellationToken ct)
    {
        if (!CanCheckForUpdates)
        {
            _logger.LogDebug("Update check skipped because the application is not installed by Velopack.");
            return null;
        }

        UpdateManager updateManager = GetUpdateManager();
        _logger.LogInformation("Checking GitHub Releases for an Atomic Art update.");
        UpdateInfo? updateInfo = await updateManager
            .CheckForUpdatesAsync()
            .WaitAsync(ct)
            .ConfigureAwait(false);

        if (updateInfo is null)
        {
            _logger.LogInformation("No Atomic Art update is available.");
            return null;
        }

        ApplicationUpdate update = new(updateInfo);
        _logger.LogInformation(
            "Atomic Art update {UpdateVersion} is available.",
            update.Version);

        return update;
    }

    public async Task DownloadUpdateAsync(
        ApplicationUpdate update,
        IProgress<int> progress,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(update);
        ArgumentNullException.ThrowIfNull(progress);

        UpdateInfo nativeUpdate = GetNativeUpdate(update);
        UpdateManager updateManager = GetUpdateManager();
        _logger.LogInformation(
            "Downloading Atomic Art update {UpdateVersion}.",
            update.Version);
        await updateManager
            .DownloadUpdatesAsync(nativeUpdate, progress.Report, ct)
            .ConfigureAwait(false);
        _logger.LogInformation(
            "Atomic Art update {UpdateVersion} was downloaded.",
            update.Version);
    }

    public void ApplyUpdateAndRestart(ApplicationUpdate update)
    {
        ArgumentNullException.ThrowIfNull(update);

        UpdateInfo nativeUpdate = GetNativeUpdate(update);
        UpdateManager updateManager = GetUpdateManager();
        _logger.LogInformation(
            "Applying Atomic Art update {UpdateVersion} and restarting.",
            update.Version);
        updateManager.ApplyUpdatesAndRestart(nativeUpdate.TargetFullRelease);
    }

    private static UpdateInfo GetNativeUpdate(ApplicationUpdate update)
    {
        if (update.NativeUpdate is not { } nativeUpdate)
        {
            throw new ArgumentException(
                "The update was not created by the Velopack update service.",
                nameof(update));
        }

        return nativeUpdate;
    }

    private UpdateManager GetUpdateManager()
    {
        lock (_syncRoot)
        {
            if (_updateManager is null)
            {
                GithubSource updateSource = new(
                    RepositoryUrl,
                    accessToken: null,
                    prerelease: false);
                _updateManager = new UpdateManager(updateSource);
            }

            return _updateManager;
        }
    }
}
