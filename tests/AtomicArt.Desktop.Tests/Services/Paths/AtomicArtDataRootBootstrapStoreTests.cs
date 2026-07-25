using System.Text;

using FluentAssertions;
using Xunit;

using AtomicArt.Desktop.Services.Paths;
using AtomicArt.Tests.Common;

namespace AtomicArt.Desktop.Tests.Services.Paths;

public sealed class AtomicArtDataRootBootstrapStoreTests
{
    [Fact]
    public async Task SaveRootDirectoryAsync_WithCustomRoot_PersistsNormalizedRoot()
    {
        string bootstrapDirectory = TestDirectories.GetUniqueDirectoryPath(
            typeof(AtomicArtDataRootBootstrapStoreTests));
        string rootDirectory = Path.Combine(bootstrapDirectory, "..", "Data");

        try
        {
            AtomicArtDataRootBootstrapStore store = new(bootstrapDirectory);

            await store.SaveRootDirectoryAsync(rootDirectory, CancellationToken.None);

            store.LoadRootDirectory().Should().Be(Path.GetFullPath(rootDirectory));
        }
        finally
        {
            TestDirectories.DeleteIfExists(bootstrapDirectory);
        }
    }

    [Fact]
    public async Task LoadRootDirectory_WithCorruptedState_ThrowsJsonException()
    {
        string bootstrapDirectory = TestDirectories.GetUniqueDirectoryPath(
            typeof(AtomicArtDataRootBootstrapStoreTests));

        try
        {
            Directory.CreateDirectory(bootstrapDirectory);
            string statePath = Path.Combine(bootstrapDirectory, "storage.json");
            await File.WriteAllTextAsync(
                statePath,
                "{invalid",
                Encoding.UTF8);
            AtomicArtDataRootBootstrapStore store = new(bootstrapDirectory);

            Action act = () => store.LoadRootDirectory();

            act.Should().Throw<System.Text.Json.JsonException>();
        }
        finally
        {
            TestDirectories.DeleteIfExists(bootstrapDirectory);
        }
    }
}
