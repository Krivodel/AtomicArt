using System.Security.Cryptography;
using System.Text;

using FluentAssertions;
using Xunit;

using AtomicArt.Desktop.Services.Paths;
using AtomicArt.Tests.Common;

namespace AtomicArt.Desktop.Tests.Services.Paths;

public sealed class DataRootMigrationRecoveryTests
{
    private static readonly DateTime FileTimestamp =
        new(2026, 7, 25, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Recover_WhenSwitchedDestinationHasNewerContent_CompletesSourceCleanup()
    {
        string testDirectory = TestDirectories.GetUniqueDirectoryPath(
            typeof(DataRootMigrationRecoveryTests));

        try
        {
            string bootstrapDirectory = Path.Combine(testDirectory, "Bootstrap");
            string sourceRoot = Path.Combine(testDirectory, "Source");
            string destinationRoot = Path.Combine(testDirectory, "Destination");
            byte[] sourceContent = Encoding.UTF8.GetBytes("source");
            byte[] destinationContent = Encoding.UTF8.GetBytes("new destination state");
            DataRootMigrationFile file = CreateManifestFile(sourceContent);
            Directory.CreateDirectory(sourceRoot);
            Directory.CreateDirectory(destinationRoot);
            await File.WriteAllBytesAsync(
                Path.Combine(sourceRoot, file.RelativePath),
                sourceContent);
            await File.WriteAllBytesAsync(
                Path.Combine(destinationRoot, file.RelativePath),
                destinationContent);
            AtomicArtDataRootBootstrapStore bootstrapStore = new(bootstrapDirectory);
            DataRootMigrationJournalStore journalStore = new(bootstrapStore);
            await bootstrapStore.SaveRootDirectoryAsync(
                destinationRoot,
                CancellationToken.None);
            await journalStore.SaveAsync(
                CreateJournal(sourceRoot, destinationRoot, file),
                CancellationToken.None);

            DataRootMigrationRecovery.Recover(bootstrapStore, journalStore);

            Directory.Exists(sourceRoot).Should().BeFalse();
            File.ReadAllBytes(Path.Combine(destinationRoot, file.RelativePath))
                .Should()
                .Equal(destinationContent);
            journalStore.Load().Should().BeNull();
        }
        finally
        {
            TestDirectories.DeleteIfExists(testDirectory);
        }
    }

    [Fact]
    public async Task Recover_WhenReadyDestinationHashDiffers_PreservesSourceAndJournal()
    {
        string testDirectory = TestDirectories.GetUniqueDirectoryPath(
            typeof(DataRootMigrationRecoveryTests));

        try
        {
            string bootstrapDirectory = Path.Combine(testDirectory, "Bootstrap");
            string sourceRoot = Path.Combine(testDirectory, "Source");
            string destinationRoot = Path.Combine(testDirectory, "Destination");
            byte[] sourceContent = Encoding.UTF8.GetBytes("source");
            byte[] destinationContent = Encoding.UTF8.GetBytes("tamper");
            DataRootMigrationFile file = CreateManifestFile(sourceContent);
            Directory.CreateDirectory(sourceRoot);
            Directory.CreateDirectory(destinationRoot);
            await File.WriteAllBytesAsync(
                Path.Combine(sourceRoot, file.RelativePath),
                sourceContent);
            await File.WriteAllBytesAsync(
                Path.Combine(destinationRoot, file.RelativePath),
                destinationContent);
            AtomicArtDataRootBootstrapStore bootstrapStore = new(bootstrapDirectory);
            DataRootMigrationJournalStore journalStore = new(bootstrapStore);
            await bootstrapStore.SaveRootDirectoryAsync(
                destinationRoot,
                CancellationToken.None);
            await journalStore.SaveAsync(
                CreateJournal(
                    sourceRoot,
                    destinationRoot,
                    file,
                    DataRootMigrationStage.ReadyToSwitch),
                CancellationToken.None);

            DataRootMigrationRecovery.Recover(bootstrapStore, journalStore);

            File.Exists(Path.Combine(sourceRoot, file.RelativePath)).Should().BeTrue();
            journalStore.Load().Should().NotBeNull();
        }
        finally
        {
            TestDirectories.DeleteIfExists(testDirectory);
        }
    }

    private static DataRootMigrationFile CreateManifestFile(byte[] content)
    {
        return new DataRootMigrationFile
        {
            RelativePath = "state.json",
            Length = content.Length,
            CreationTimeUtc = FileTimestamp,
            LastWriteTimeUtc = FileTimestamp,
            Attributes = FileAttributes.Normal,
            Sha256 = Convert.ToHexString(SHA256.HashData(content))
        };
    }

    private static DataRootMigrationJournal CreateJournal(
        string sourceRoot,
        string destinationRoot,
        DataRootMigrationFile file,
        DataRootMigrationStage stage = DataRootMigrationStage.Switched)
    {
        return new DataRootMigrationJournal
        {
            SourceRootDirectory = sourceRoot,
            DestinationRootDirectory = destinationRoot,
            Stage = stage,
            Directories = Array.Empty<string>(),
            Files = new List<DataRootMigrationFile> { file }
        };
    }
}
