namespace AtomicArt.Desktop.Services.Paths;

public sealed record DataRootMigrationProgress
{
    public required DataRootMigrationProgressStage Stage { get; init; }
    public required long CompletedBytes { get; init; }
    public required long TotalBytes { get; init; }
    public required int CompletedFiles { get; init; }
    public required int TotalFiles { get; init; }
    public double Percentage => CalculatePercentage();

    private const double TransferMaximumPercentage = 95;
    private const double SwitchingPercentage = 96;
    private const double CleaningPercentage = 98;
    private const double CompletedPercentage = 100;

    private double CalculatePercentage()
    {
        if (Stage == DataRootMigrationProgressStage.Completed)
        {
            return CompletedPercentage;
        }

        if (Stage == DataRootMigrationProgressStage.Switching)
        {
            return SwitchingPercentage;
        }

        if (Stage == DataRootMigrationProgressStage.Cleaning)
        {
            return CleaningPercentage;
        }

        if (TotalBytes <= 0)
        {
            return 0;
        }

        return Math.Clamp(
            (double)CompletedBytes / TotalBytes * TransferMaximumPercentage,
            0,
            TransferMaximumPercentage);
    }
}
