using Microsoft.Extensions.Logging;

using AtomicArt.Desktop.Services.Gallery.State;
using AtomicArt.Desktop.Services.Settings;

namespace AtomicArt.Desktop.Services.State;

public sealed class AppStateBootstrapper : IAppStateBootstrapper
{
    private const string GenerationPanelsSectionName = "generation panels";

    private readonly ISettingsStateService _settingsStateService;
    private readonly IGalleryStateService _galleryStateService;
    private readonly IStateWriteScheduler _writeScheduler;
    private readonly ILogger<AppStateBootstrapper> _logger;

    public AppStateBootstrapper(
        ISettingsStateService settingsStateService,
        IGalleryStateService galleryStateService,
        IStateWriteScheduler writeScheduler,
        ILogger<AppStateBootstrapper> logger)
    {
        _settingsStateService = settingsStateService
            ?? throw new ArgumentNullException(nameof(settingsStateService));
        _galleryStateService = galleryStateService
            ?? throw new ArgumentNullException(nameof(galleryStateService));
        _writeScheduler = writeScheduler ?? throw new ArgumentNullException(nameof(writeScheduler));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task RestoreAsync(IAppStateRestoreTarget target, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(target);
        _logger.LogInformation("Atomic Art state restore started.");

        await RestoreSectionAsync(
            SettingsStateSection.KeyValue,
            () => _settingsStateService.ApplySavedSettingsAsync(ct),
            ct).ConfigureAwait(false);
        await RestoreSectionAsync(
            GenerationPanelsSectionName,
            () => target.RestoreGenerationPanelsAsync(ct),
            ct).ConfigureAwait(false);
        await RestoreSectionAsync(
            GalleryStateSection.KeyValue,
            () => RestoreGalleryAsync(target, ct),
            ct).ConfigureAwait(false);
        _logger.LogInformation("Atomic Art state restore completed.");
    }

    public async Task FlushAsync(IAppStateFlushTarget target, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(target);
        _logger.LogInformation("Atomic Art state flush started.");

        await CommitPendingStateAsync(target, ct).ConfigureAwait(false);
        await FlushCoreAsync(ct).ConfigureAwait(false);
        _logger.LogInformation("Atomic Art state flush completed.");
    }

    private async Task RestoreGalleryAsync(IAppStateRestoreTarget target, CancellationToken ct)
    {
        GalleryState state = await _galleryStateService.LoadAsync(ct).ConfigureAwait(false);
        await target.RestoreGalleryAsync(state.Items, ct).ConfigureAwait(false);
    }

    private async Task RestoreSectionAsync(
        string sectionName,
        Func<Task> restore,
        CancellationToken ct)
    {
        try
        {
            await restore().ConfigureAwait(false);
            _logger.LogInformation(
                "App state section {SectionName} restored.",
                sectionName);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to restore app state section {SectionName}.",
                sectionName);
        }
    }

    private async Task CommitPendingStateAsync(IAppStateFlushTarget target, CancellationToken ct)
    {
        try
        {
            await target.CommitPendingStateAsync(ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to commit pending app state before flush.");
        }
    }

    private async Task FlushCoreAsync(CancellationToken ct)
    {
        try
        {
            await _writeScheduler.FlushAsync(ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to flush app state writes.");
        }
    }
}
