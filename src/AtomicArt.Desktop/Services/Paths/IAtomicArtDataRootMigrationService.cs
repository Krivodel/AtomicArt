namespace AtomicArt.Desktop.Services.Paths;

public interface IAtomicArtDataRootMigrationService
{
    Task MigrateAsync(
        string destinationRootDirectory,
        IProgress<DataRootMigrationProgress> progress,
        CancellationToken ct);
}
