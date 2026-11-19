using AtomicArt.Desktop.Services.Gallery;
using AtomicArt.Desktop.Services.State;

namespace AtomicArt.Desktop.Services.Updates;

public sealed class ApplicationUpdateRestartCoordinator :
    IApplicationUpdateRestartCoordinator,
    IApplicationUpdateRestartAttachmentService
{
    private readonly IApplicationStateFlushService _stateFlushService;
    private readonly IImageViewerWindowCloser _viewerWindowCloser;
    private readonly IApplicationUpdateService _updateService;
    private readonly object _syncRoot = new();
    private IAppStateFlushTarget? _stateFlushTarget;

    public ApplicationUpdateRestartCoordinator(
        IApplicationStateFlushService stateFlushService,
        IImageViewerWindowCloser viewerWindowCloser,
        IApplicationUpdateService updateService)
    {
        _stateFlushService = stateFlushService
            ?? throw new ArgumentNullException(nameof(stateFlushService));
        _viewerWindowCloser = viewerWindowCloser
            ?? throw new ArgumentNullException(nameof(viewerWindowCloser));
        _updateService = updateService
            ?? throw new ArgumentNullException(nameof(updateService));
    }

    public async Task ApplyAndRestartAsync(ApplicationUpdate update, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(update);

        IAppStateFlushTarget stateFlushTarget;

        lock (_syncRoot)
        {
            stateFlushTarget = _stateFlushTarget
                ?? throw new InvalidOperationException(
                    "Application update restart target has not been attached.");
        }

        await _stateFlushService
            .FlushAsync(stateFlushTarget, ct)
            .ConfigureAwait(false);
        await _viewerWindowCloser.CloseAllAsync(ct).ConfigureAwait(false);
        _updateService.ApplyUpdateAndRestart(update);
    }

    public void Attach(IAppStateFlushTarget stateFlushTarget)
    {
        ArgumentNullException.ThrowIfNull(stateFlushTarget);

        lock (_syncRoot)
        {
            _stateFlushTarget = stateFlushTarget;
        }
    }
}
