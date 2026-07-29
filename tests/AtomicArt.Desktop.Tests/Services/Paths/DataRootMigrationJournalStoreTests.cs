using FluentAssertions;
using Xunit;

using AtomicArt.Desktop.Services.Paths;
using AtomicArt.Tests.Common;

namespace AtomicArt.Desktop.Tests.Services.Paths;

public sealed class DataRootMigrationJournalStoreTests
{
    [Fact]
    public async Task SaveAsync_WithUnicodePaths_WritesReadableJson()
    {
        string bootstrapDirectory = TestDirectories.GetUniqueDirectoryPath(
            typeof(DataRootMigrationJournalStoreTests));

        try
        {
            AtomicArtDataRootBootstrapStore bootstrapStore = new(bootstrapDirectory);
            DataRootMigrationJournalStore journalStore = new(bootstrapStore);
            DataRootMigrationJournal journal = new()
            {
                SourceRootDirectory = Path.Combine(bootstrapDirectory, "Источник"),
                DestinationRootDirectory = Path.Combine(bootstrapDirectory, "Назначение"),
                Stage = DataRootMigrationStage.Copying,
                Directories = new List<string> { "Каталог" },
                Files = new List<DataRootMigrationFile>()
            };

            await journalStore.SaveAsync(journal, CancellationToken.None);

            string json = await File.ReadAllTextAsync(
                Path.Combine(bootstrapDirectory, "storage-migration.json"),
                CancellationToken.None);
            json.Should().Contain("Источник");
            json.Should().Contain("Назначение");
            json.Should().Contain("Каталог");
            json.Should().NotContain("\\u");
        }
        finally
        {
            TestDirectories.DeleteIfExists(bootstrapDirectory);
        }
    }
}
