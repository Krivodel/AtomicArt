namespace AtomicArt.Desktop.Services.Paths;

public enum DataRootMigrationProgressStage
{
    Preparing,
    Copying,
    Verifying,
    Switching,
    Cleaning,
    Completed
}
