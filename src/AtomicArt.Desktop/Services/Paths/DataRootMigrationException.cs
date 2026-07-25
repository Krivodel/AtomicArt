namespace AtomicArt.Desktop.Services.Paths;

public sealed class DataRootMigrationException : IOException
{
    public DataRootMigrationException(Exception innerException)
        : base("Atomic Art data root migration failed.", innerException)
    {
    }
}
