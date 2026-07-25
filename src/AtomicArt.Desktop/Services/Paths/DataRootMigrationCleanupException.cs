namespace AtomicArt.Desktop.Services.Paths;

public sealed class DataRootMigrationCleanupException : IOException
{
    public DataRootMigrationCleanupException(Exception innerException)
        : base(
            "The data root was switched, but the previous directory could not be fully cleaned.",
            innerException)
    {
    }
}
