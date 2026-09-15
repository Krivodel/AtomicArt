namespace AtomicArt.Desktop.Services.Dlss5;

public interface IDlss5ModuleInstaller
{
    bool IsInstalled { get; }

    Task EnsureInstalledAsync(
        IProgress<Dlss5ModuleInstallProgress>? progress,
        CancellationToken ct);
}
