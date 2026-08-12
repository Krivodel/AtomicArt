using System.Text;

using FluentAssertions;
using Xunit;

using AtomicArt.Desktop.Services.Paths;
using AtomicArt.Tests.Common;

namespace AtomicArt.Desktop.Tests.Services.Paths;

public sealed class AtomicArtDataRootBootstrapStoreTests
{
    [Fact]
    public void ShouldOfferInitialRootDirectorySelection_WithFreshDirectories_ReturnsTrue()
    {
        string bootstrapDirectory = TestDirectories.GetUniqueDirectoryPath(
            typeof(AtomicArtDataRootBootstrapStoreTests));
        string rootDirectory = string.Concat(bootstrapDirectory, "-data");

        try
        {
            AtomicArtDataRootBootstrapStore store = new(
                bootstrapDirectory,
                rootDirectory);

            bool result = store.ShouldOfferInitialRootDirectorySelection();

            result.Should().BeTrue();
        }
        finally
        {
            TestDirectories.DeleteIfExists(bootstrapDirectory);
            TestDirectories.DeleteIfExists(rootDirectory);
        }
    }

    [Fact]
    public void ShouldOfferInitialRootDirectorySelection_WithExistingDataDirectory_ReturnsFalse()
    {
        string bootstrapDirectory = TestDirectories.GetUniqueDirectoryPath(
            typeof(AtomicArtDataRootBootstrapStoreTests));
        string rootDirectory = string.Concat(bootstrapDirectory, "-data");

        try
        {
            Directory.CreateDirectory(rootDirectory);
            AtomicArtDataRootBootstrapStore store = new(
                bootstrapDirectory,
                rootDirectory);

            bool result = store.ShouldOfferInitialRootDirectorySelection();

            result.Should().BeFalse();
        }
        finally
        {
            TestDirectories.DeleteIfExists(bootstrapDirectory);
            TestDirectories.DeleteIfExists(rootDirectory);
        }
    }

    [Fact]
    public async Task ShouldOfferInitialRootDirectorySelection_WithPersistedRoot_ReturnsFalse()
    {
        string bootstrapDirectory = TestDirectories.GetUniqueDirectoryPath(
            typeof(AtomicArtDataRootBootstrapStoreTests));
        string rootDirectory = string.Concat(bootstrapDirectory, "-data");

        try
        {
            AtomicArtDataRootBootstrapStore store = new(
                bootstrapDirectory,
                rootDirectory);
            await store.SaveRootDirectoryAsync(rootDirectory, CancellationToken.None);

            bool result = store.ShouldOfferInitialRootDirectorySelection();

            result.Should().BeFalse();
        }
        finally
        {
            TestDirectories.DeleteIfExists(bootstrapDirectory);
            TestDirectories.DeleteIfExists(rootDirectory);
        }
    }

    [Fact]
    public async Task ShouldOfferInitialRootDirectorySelection_WhenPendingThenCompleted_TracksSelectionState()
    {
        string bootstrapDirectory = TestDirectories.GetUniqueDirectoryPath(
            typeof(AtomicArtDataRootBootstrapStoreTests));
        string rootDirectory = string.Concat(bootstrapDirectory, "-data");

        try
        {
            AtomicArtDataRootBootstrapStore store = new(
                bootstrapDirectory,
                rootDirectory);
            await store.MarkInitialRootDirectorySelectionPendingAsync(
                rootDirectory,
                CancellationToken.None);

            bool pendingResult = store.ShouldOfferInitialRootDirectorySelection();
            await store.MarkInitialRootDirectorySelectionCompletedAsync(
                rootDirectory,
                CancellationToken.None);
            bool completedResult = store.ShouldOfferInitialRootDirectorySelection();

            pendingResult.Should().BeTrue();
            completedResult.Should().BeFalse();
        }
        finally
        {
            TestDirectories.DeleteIfExists(bootstrapDirectory);
            TestDirectories.DeleteIfExists(rootDirectory);
        }
    }

    [Fact]
    public async Task SaveRootDirectoryAsync_WithUnicodeRoot_PersistsReadableNormalizedRoot()
    {
        string bootstrapDirectory = TestDirectories.GetUniqueDirectoryPath(
            typeof(AtomicArtDataRootBootstrapStoreTests));
        string rootDirectory = Path.Combine(bootstrapDirectory, "..", "Данные");

        try
        {
            AtomicArtDataRootBootstrapStore store = new(bootstrapDirectory);

            await store.SaveRootDirectoryAsync(rootDirectory, CancellationToken.None);

            string json = await File.ReadAllTextAsync(
                Path.Combine(bootstrapDirectory, "storage.json"),
                CancellationToken.None);
            json.Should().Contain("Данные");
            json.Should().NotContain("\\u");
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
