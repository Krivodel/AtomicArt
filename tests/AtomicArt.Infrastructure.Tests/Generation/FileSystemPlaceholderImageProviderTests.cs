using Microsoft.Extensions.Options;

using FluentAssertions;
using Xunit;

using AtomicArt.Application.Features.Generation.Models;
using AtomicArt.Contracts.Generation;
using AtomicArt.Infrastructure.Generation;
using AtomicArt.Tests.Common;
using AtomicArt.Tests.Common.Generation;

namespace AtomicArt.Infrastructure.Tests.Generation;

public sealed class FileSystemPlaceholderImageProviderTests
{
    [Fact]
    public async Task GetNextAsync_WithSupportedImageWithoutExtension_ReturnsImage()
    {
        using TemporaryDirectory directory = new(
            typeof(FileSystemPlaceholderImageProviderTests),
            nameof(GetNextAsync_WithSupportedImageWithoutExtension_ReturnsImage));
        string path = Path.Combine(directory.DirectoryPath, "any name");
        await File.WriteAllBytesAsync(
            path,
            GenerationImageTestData.MinimalPngBytes,
            CancellationToken.None);
        FileSystemPlaceholderImageProvider provider = CreateProvider(directory.DirectoryPath);

        PlaceholderImage image = await provider.GetNextAsync("test", 0, CancellationToken.None);

        image.ContentType.Should().Be(GenerationImageContentTypes.Png);
        image.Content.Should().Equal(GenerationImageTestData.MinimalPngBytes);
    }

    [Fact]
    public async Task GetNextAsync_WithUnsupportedFiles_SkipsThem()
    {
        using TemporaryDirectory directory = new(
            typeof(FileSystemPlaceholderImageProviderTests),
            nameof(GetNextAsync_WithUnsupportedFiles_SkipsThem));
        await File.WriteAllTextAsync(
            Path.Combine(directory.DirectoryPath, "notes.txt"),
            "not an image",
            CancellationToken.None);
        await File.WriteAllBytesAsync(
            Path.Combine(directory.DirectoryPath, "image.bin"),
            GenerationImageTestData.MinimalPngBytes,
            CancellationToken.None);
        FileSystemPlaceholderImageProvider provider = CreateProvider(directory.DirectoryPath);

        PlaceholderImage image = await provider.GetNextAsync("test", 0, CancellationToken.None);

        image.ContentType.Should().Be(GenerationImageContentTypes.Png);
    }

    [Fact]
    public async Task GetNextAsync_WithEmptyDirectory_ThrowsProviderException()
    {
        using TemporaryDirectory directory = new(
            typeof(FileSystemPlaceholderImageProviderTests),
            nameof(GetNextAsync_WithEmptyDirectory_ThrowsProviderException));
        FileSystemPlaceholderImageProvider provider = CreateProvider(directory.DirectoryPath);

        Func<Task> act = () => provider.GetNextAsync("test", 0, CancellationToken.None);

        await act.Should().ThrowAsync<ImageGenerationProviderException>()
            .Where(exception => exception.FailureKind == ImageGenerationProviderFailureKind.Unavailable);
    }

    [Fact]
    public async Task GetNextAsync_WithImageOverLimit_SkipsFile()
    {
        using TemporaryDirectory directory = new(
            typeof(FileSystemPlaceholderImageProviderTests),
            nameof(GetNextAsync_WithImageOverLimit_SkipsFile));
        string path = Path.Combine(directory.DirectoryPath, "large");
        await File.WriteAllBytesAsync(
            path,
            GenerationImageTestData.MinimalPngBytes,
            CancellationToken.None);
        FileSystemPlaceholderImageProvider provider = CreateProvider(
            directory.DirectoryPath,
            maxImageBytes: 4);

        Func<Task> act = () => provider.GetNextAsync("test", 0, CancellationToken.None);

        await act.Should().ThrowAsync<ImageGenerationProviderException>()
            .Where(exception => exception.FailureKind == ImageGenerationProviderFailureKind.Unavailable);
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

}
