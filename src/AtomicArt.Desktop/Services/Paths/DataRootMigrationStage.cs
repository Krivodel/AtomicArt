namespace AtomicArt.Desktop.Services.Paths;

internal enum DataRootMigrationStage
{
    Copying,
    ReadyToSwitch,
    Switched,
    CleaningSource
}
