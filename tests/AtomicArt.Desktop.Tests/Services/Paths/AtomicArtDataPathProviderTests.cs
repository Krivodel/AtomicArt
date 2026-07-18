using FluentAssertions;
using Xunit;

using AtomicArt.Desktop.Services.Paths;
using AtomicArt.Tests.Common;

namespace AtomicArt.Desktop.Tests.Services.Paths;

public sealed class AtomicArtDataPathProviderTests
{
    [Fact]
    public void Constructor_WithTestRoot_ReturnsKnownDirectoriesUnderRoot()
    {
        string rootDirectory = CreateTempRoot();

        AtomicArtDataPathProvider provider = new(rootDirectory);

        provider.RootDirectory.Should().Be(Path.GetFullPath(rootDirectory));
        provider.ArtDirectory.Should().Be(Path.Combine(provider.RootDirectory, "Art"));
        provider.SecretsDirectory.Should().Be(Path.Combine(provider.RootDirectory, "Secrets"));
        provider.ThumbnailsDirectory.Should().Be(Path.Combine(provider.RootDirectory, "Thumbnails"));
        provider.StateDirectory.Should().Be(Path.Combine(provider.RootDirectory, "State"));
        provider.StateAttachmentsDirectory.Should().Be(Path.Combine(
            provider.RootDirectory,
            "State",
            "Attachments"));
    }

    [Fact]
    public void EnsureDirectoryExists_WithKnownDirectory_CreatesDirectory()
    {
        string rootDirectory = CreateTempRoot();

        try
        {
            AtomicArtDataPathProvider provider = new(rootDirectory);

            provider.EnsureDirectoryExists(provider.StateAttachmentsDirectory);

            Directory.Exists(provider.StateAttachmentsDirectory).Should().BeTrue();
        }
        finally
        {
            TestDirectories.DeleteIfExists(rootDirectory);
        }
    }

    [Fact]
    public void EnsureDirectoryExists_WithUnknownDirectory_ThrowsInvalidOperationException()
    {
        string rootDirectory = CreateTempRoot();
        AtomicArtDataPathProvider provider = new(rootDirectory);
        string unknownDirectory = Path.Combine(provider.RootDirectory, "Unknown");

        Action act = () => provider.EnsureDirectoryExists(unknownDirectory);

        act.Should().Throw<InvalidOperationException>();
        Directory.Exists(unknownDirectory).Should().BeFalse();
    }

    private static string CreateTempRoot()
    {
        return Path.Combine(
            Path.GetTempPath(),
            "AtomicArt.AtomicArtDataPathProviderTests",
            Guid.NewGuid().ToString("N"));
    }

}
