using Microsoft.Extensions.Options;

using FluentAssertions;
using Xunit;

using AtomicArt.Application.Features.Generation.Models;
using AtomicArt.Contracts.Generation;
using AtomicArt.Infrastructure.Generation;

namespace AtomicArt.Infrastructure.Tests.Generation;

public sealed class FileSystemPlaceholderImageProviderTests
{
    [Fact]
    public async Task GetNextAsync_WithSupportedImageWithoutExtension_ReturnsImage()
    {
        string directory = CreateCleanDirectory(nameof(GetNextAsync_WithSupportedImageWithoutExtension_ReturnsImage));

        try
        {
            string path = Path.Combine(directory, "any name");
            await File.WriteAllBytesAsync(path, CreatePngBytes(), CancellationToken.None);
            FileSystemPlaceholderImageProvider provider = CreateProvider(directory);

            PlaceholderImage image = await provider.GetNextAsync("test", 0, CancellationToken.None);

            image.ContentType.Should().Be(GenerationImageContentTypes.Png);
            image.Content.Should().Equal(CreatePngBytes());
        }
        finally
        {
            DeleteDirectoryIfExists(directory);
        }
    }

    [Fact]
    public async Task GetNextAsync_WithUnsupportedFiles_SkipsThem()
    {
        string directory = CreateCleanDirectory(nameof(GetNextAsync_WithUnsupportedFiles_SkipsThem));

        try
        {
            await File.WriteAllTextAsync(Path.Combine(directory, "notes.txt"), "not an image", CancellationToken.None);
            await File.WriteAllBytesAsync(Path.Combine(directory, "image.bin"), CreatePngBytes(), CancellationToken.None);
            FileSystemPlaceholderImageProvider provider = CreateProvider(directory);

            PlaceholderImage image = await provider.GetNextAsync("test", 0, CancellationToken.None);

            image.ContentType.Should().Be(GenerationImageContentTypes.Png);
        }
        finally
        {
            DeleteDirectoryIfExists(directory);
        }
    }

    [Fact]
    public async Task GetNextAsync_WithEmptyDirectory_ThrowsProviderException()
    {
        string directory = CreateCleanDirectory(nameof(GetNextAsync_WithEmptyDirectory_ThrowsProviderException));

        try
        {
            FileSystemPlaceholderImageProvider provider = CreateProvider(directory);

            Func<Task> act = () => provider.GetNextAsync("test", 0, CancellationToken.None);

            await act.Should().ThrowAsync<ImageGenerationProviderException>()
                .Where(exception => exception.FailureKind == ImageGenerationProviderFailureKind.Unavailable);
        }
        finally
        {
            DeleteDirectoryIfExists(directory);
        }
    }

    [Fact]
    public async Task GetNextAsync_WithImageOverLimit_SkipsFile()
    {
        string directory = CreateCleanDirectory(nameof(GetNextAsync_WithImageOverLimit_SkipsFile));

        try
        {
            string path = Path.Combine(directory, "large");
            await File.WriteAllBytesAsync(path, CreatePngBytes(), CancellationToken.None);
            FileSystemPlaceholderImageProvider provider = CreateProvider(directory, maxImageBytes: 4);

            Func<Task> act = () => provider.GetNextAsync("test", 0, CancellationToken.None);

            await act.Should().ThrowAsync<ImageGenerationProviderException>()
                .Where(exception => exception.FailureKind == ImageGenerationProviderFailureKind.Unavailable);
        }
        finally
        {
            DeleteDirectoryIfExists(directory);
        }
    }

    private static FileSystemPlaceholderImageProvider CreateProvider(
        string imagesDirectory,
        long maxImageBytes = TestGenerationOptions.DefaultMaxImageBytes)
    {
        return new FileSystemPlaceholderImageProvider(
            Options.Create(new TestGenerationOptions
            {
                Enabled = true,
                ImagesDirectory = imagesDirectory,
                MaxImageBytes = maxImageBytes
            }));
    }

    private static byte[] CreatePngBytes()
    {
        return [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00];
    }

    private static string CreateCleanDirectory(string testName)
    {
        string directory = Path.Combine(
            Path.GetTempPath(),
            "AtomicArt.Infrastructure.Tests",
            nameof(FileSystemPlaceholderImageProviderTests),
            testName,
            Guid.NewGuid().ToString("N"));

        DeleteDirectoryIfExists(directory);
        Directory.CreateDirectory(directory);

        return directory;
    }

    private static void DeleteDirectoryIfExists(string directory)
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, true);
        }
    }
}
