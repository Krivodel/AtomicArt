using Microsoft.Extensions.Logging.Abstractions;

using FluentAssertions;
using Xunit;

using AtomicArt.Desktop.Services.Gallery.State;
using AtomicArt.Desktop.Services.Paths;
using AtomicArt.Desktop.Tests.TestDoubles;
using AtomicArt.Tests.Common;

namespace AtomicArt.Desktop.Tests.Services.Gallery.State;

public sealed class GalleryStatePathConverterTests
{
    private const string ModelId = "nano-banana-2";

    [Fact]
    public void GetStoragePath_WithValidatedAbsolutePath_ReturnsPortableRelativePath()
    {
        string rootDirectory = TestDirectories.GetUniqueDirectoryPath(
            typeof(GalleryStatePathConverterTests));
        AtomicArtDataPathProvider pathProvider = new(rootDirectory);
        string imagePath = Path.Combine(pathProvider.ArtDirectory, "generation.png");
        GalleryStatePathConverter converter = CreateConverter(
            pathProvider,
            new PassthroughTrustedImageFileService());

        string? storedPath = converter.GetStoragePath(imagePath);

        storedPath.Should().Be("Art/generation.png");
    }

    [Fact]
    public void GetRuntimeImagePath_WithPortableRelativePath_ReturnsAbsolutePath()
    {
        string rootDirectory = TestDirectories.GetUniqueDirectoryPath(
            typeof(GalleryStatePathConverterTests));
        AtomicArtDataPathProvider pathProvider = new(rootDirectory);
        GalleryStatePathConverter converter = CreateConverter(
            pathProvider,
            new PassthroughTrustedImageFileService());
        string expectedPath = Path.Combine(pathProvider.ArtDirectory, "generation.png");

        string? runtimePath = converter.GetRuntimeImagePath(
            "Art/generation.png",
            ModelId);

        runtimePath.Should().Be(expectedPath);
    }

    [Fact]
    public void GetRuntimeImagePath_WithRelocatedLegacyPath_ReturnsPathUnderCurrentRoot()
    {
        string rootDirectory = TestDirectories.GetUniqueDirectoryPath(
            typeof(GalleryStatePathConverterTests));

        try
        {
            AtomicArtDataPathProvider pathProvider = new(rootDirectory);
            Directory.CreateDirectory(pathProvider.ArtDirectory);
            string currentImagePath = Path.Combine(
                pathProvider.ArtDirectory,
                "generation.png");
            File.WriteAllBytes(currentImagePath, [0x01]);
            string previousRootDirectory = string.Concat(rootDirectory, "-previous");
            string legacyImagePath = Path.Combine(
                previousRootDirectory,
                "Art",
                "generation.png");
            GalleryStatePathConverter converter = CreateConverter(
                pathProvider,
                new ExistingFileTrustedImageFileService());

            string? runtimePath = converter.GetRuntimeImagePath(
                legacyImagePath,
                ModelId);

            runtimePath.Should().Be(currentImagePath);
        }
        finally
        {
            TestDirectories.DeleteIfExists(rootDirectory);
        }
    }

    private static GalleryStatePathConverter CreateConverter(
        AtomicArtDataPathProvider pathProvider,
        TrustedImageFileServiceTestDouble trustedImageFileService)
    {
        return new GalleryStatePathConverter(
            pathProvider,
            trustedImageFileService,
            NullLogger<GalleryStatePathConverter>.Instance);
    }
}
