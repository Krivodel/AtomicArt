namespace AtomicArt.Desktop.Services.Paths;

internal static class DataRootMigrationRecovery
{
    internal static void Recover(
        AtomicArtDataRootBootstrapStore bootstrapStore,
        DataRootMigrationJournalStore journalStore)
    {
        ArgumentNullException.ThrowIfNull(bootstrapStore);
        ArgumentNullException.ThrowIfNull(journalStore);

        DataRootMigrationJournal? journal = journalStore.Load();

        if (journal is null)
        {
            return;
        }

        string configuredRoot = Path.TrimEndingDirectorySeparator(
            bootstrapStore.LoadRootDirectory());
        string destinationRoot = Path.TrimEndingDirectorySeparator(
            Path.GetFullPath(journal.DestinationRootDirectory));
        string sourceRoot = Path.TrimEndingDirectorySeparator(
            Path.GetFullPath(journal.SourceRootDirectory));
        DataRootFileTransfer fileTransfer = new();

        if (string.Equals(
                configuredRoot,
                destinationRoot,
                StringComparison.OrdinalIgnoreCase))
        {
            if (journal.Stage == DataRootMigrationStage.Copying)
            {
                return;
            }

            if (journal.Stage == DataRootMigrationStage.ReadyToSwitch
                && !fileTransfer.FilesMatchManifest(destinationRoot, journal.Files))
            {
                return;
            }

            fileTransfer.DeleteSourceFiles(
                journal.SourceRootDirectory,
                journal.Files,
                journal.Directories);
            journalStore.Delete();
            return;
        }

        if (!string.Equals(
                configuredRoot,
                sourceRoot,
                StringComparison.OrdinalIgnoreCase)
            || journal.Stage is DataRootMigrationStage.Switched
                or DataRootMigrationStage.CleaningSource)
        {
            return;
        }

        if (journal.Stage == DataRootMigrationStage.ReadyToSwitch)
        {
            if (!fileTransfer.FilesMatchManifest(destinationRoot, journal.Files))
            {
                return;
            }

            fileTransfer.DeleteVerifiedCopiedFiles(
                destinationRoot,
                journal.Files,
                journal.Directories);
        }
        else
        {
            fileTransfer.DeleteCopiedFiles(
                destinationRoot,
                journal.Files,
                journal.Directories);
        }

        journalStore.Delete();
    }
}
