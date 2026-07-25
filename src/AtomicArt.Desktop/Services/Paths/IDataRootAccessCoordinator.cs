namespace AtomicArt.Desktop.Services.Paths;

public interface IDataRootAccessCoordinator
{
    Task<DataRootAccessLease> AcquireAccessAsync(CancellationToken ct);
    Task<DataRootMigrationLease> BeginMigrationAsync(CancellationToken ct);
}
