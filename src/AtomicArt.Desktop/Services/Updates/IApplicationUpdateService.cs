namespace AtomicArt.Desktop.Services.Updates;

public interface IApplicationUpdateService
{
    bool CanCheckForUpdates { get; }

    Task<ApplicationUpdate?> CheckForUpdateAsync(CancellationToken ct);

    Task DownloadUpdateAsync(
        ApplicationUpdate update,
        IProgress<int> progress,
        CancellationToken ct);

    void ApplyUpdateAndRestart(ApplicationUpdate update);
}
