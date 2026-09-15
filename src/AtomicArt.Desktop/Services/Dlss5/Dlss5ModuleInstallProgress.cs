namespace AtomicArt.Desktop.Services.Dlss5;

public sealed record Dlss5ModuleInstallProgress(
    long ReceivedBytes,
    long TotalBytes,
    bool IsIndeterminate)
{
    public int Percent => TotalBytes <= PercentageMinimum
        ? PercentageMinimum
        : (int)Math.Clamp(
            ReceivedBytes * PercentageMaximum / TotalBytes,
            PercentageMinimum,
            PercentageMaximum);

    private const int PercentageMinimum = 0;
    private const int PercentageMaximum = 100;
}
