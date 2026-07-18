using Pica.Viewer.Services;

namespace AtomicArt.Desktop.Services.State;

public sealed class ApplicationStateFlushService : IApplicationStateFlushService
{
    private readonly IAppStateBootstrapper _appStateBootstrapper;
    private readonly IClipboardImageWriter _clipboardImageWriter;

    public ApplicationStateFlushService(
        IAppStateBootstrapper appStateBootstrapper,
        IClipboardImageWriter clipboardImageWriter)
    {
        _appStateBootstrapper = appStateBootstrapper
            ?? throw new ArgumentNullException(nameof(appStateBootstrapper));
        _clipboardImageWriter = clipboardImageWriter
            ?? throw new ArgumentNullException(nameof(clipboardImageWriter));
    }

    public async Task FlushAsync(IAppStateFlushTarget target, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(target);

        Task stateFlushTask = _appStateBootstrapper.FlushAsync(target, ct);
        Task clipboardFlushTask = _clipboardImageWriter.FlushAsync(ct);
        await Task.WhenAll(stateFlushTask, clipboardFlushTask).ConfigureAwait(false);
    }
}
