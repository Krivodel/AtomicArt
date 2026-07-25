using AtomicArt.Desktop.Services.State;

namespace AtomicArt.Desktop.Services.Paths;

public interface IDataRootMigrationTarget : IAppStateFlushTarget
{
    Task RebaseDataRootAsync(
        string sourceRootDirectory,
        string destinationRootDirectory,
        CancellationToken ct);
}
