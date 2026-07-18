namespace AtomicArt.Desktop.Services.Updates;

public interface IApplicationUpdateRestartCoordinator
{
    Task ApplyAndRestartAsync(ApplicationUpdate update, CancellationToken ct);
}
