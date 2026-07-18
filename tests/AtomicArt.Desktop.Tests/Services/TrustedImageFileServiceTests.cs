using Microsoft.Extensions.Logging.Abstractions;

using FluentAssertions;
using Xunit;

using AtomicArt.Desktop.Services;
using AtomicArt.Desktop.Services.Generation;
using AtomicArt.Desktop.Services.Paths;
using AtomicArt.Desktop.Tests.Services.Generation;

namespace AtomicArt.Desktop.Tests.Services;

public sealed class TrustedImageFileServiceTests
{
    private const long PreviousTrustedImageBytes = 10L * 1_048_576L;
    private const long TestImageBytes = PreviousTrustedImageBytes + 1L;
    private const string ModelId = "test-model";
    private const string TestDirectoryNamePrefix = "trusted-large-placeholder-test-";
    private const string TestFileName = "trusted-large-placeholder.png";

    private static readonly byte[] PngSignature =
    [
        0x89,
        0x50,
        0x4E,
        0x47,
        0x0D,
        0x0A,
        0x1A,
        0x0A
    ];

    [Fact]
    public void GetTrustedImagePathOrDefault_WithPngLargerThanPreviousLimitInsideArt_ReturnsTrustedPath()
    {
        string rootDirectory = Path.Combine(
            Path.GetTempPath(),
            "AtomicArt.TrustedImageFileServiceTests",
            Guid.NewGuid().ToString("N"));
        AtomicArtDataPathProvider pathProvider = new(rootDirectory);
        TrustedImageFileService service = new(
            pathProvider,
            GenerationImageFormatRegistryTestFactory.Create(),
            NullLogger<TrustedImageFileService>.Instance);
        string testDirectory = Path.Combine(
            pathProvider.ArtDirectory,
            $"{TestDirectoryNamePrefix}{Guid.NewGuid():N}");
        Directory.Exists(testDirectory).Should().BeFalse();
        Directory.CreateDirectory(testDirectory);
        string imagePath = Path.Combine(testDirectory, TestFileName);

        try
        {
            CreatePngFile(imagePath, TestImageBytes);

            string? trustedPath = service.GetTrustedImagePathOrDefault(imagePath, ModelId);

            trustedPath.Should().Be(Path.GetFullPath(imagePath));
            TestImageBytes.Should().BeGreaterThan(PreviousTrustedImageBytes);
            TestImageBytes.Should().BeLessThan(GenerationImageContentValidator.DefaultMaxImageBytes);
        }
        finally
        {
            DeleteFileIfExists(imagePath);
            DeleteDirectoryIfExists(testDirectory);
            DeleteDirectoryIfExists(rootDirectory);
        }
    }

    private static void CreatePngFile(string path, long length)
    {
        using FileStream stream = new(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        stream.Write(PngSignature, 0, PngSignature.Length);
        stream.SetLength(length);
    }

    private static void DeleteFileIfExists(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private static void DeleteDirectoryIfExists(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }
}
