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
    private const string ProviderModelId = "test";

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
        PlaceholderImage image = await GetNextImageAsync(directory.DirectoryPath);

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
        PlaceholderImage image = await GetNextImageAsync(directory.DirectoryPath);

        image.ContentType.Should().Be(GenerationImageContentTypes.Png);
    }

    [Fact]
    public async Task GetNextAsync_WithEmptyDirectory_ThrowsProviderException()
    {
        using TemporaryDirectory directory = new(
            typeof(FileSystemPlaceholderImageProviderTests),
            nameof(GetNextAsync_WithEmptyDirectory_ThrowsProviderException));

        await AssertUnavailableAsync(() => GetNextImageAsync(directory.DirectoryPath));
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
        await AssertUnavailableAsync(() => GetNextImageAsync(
            directory.DirectoryPath,
            maxImageBytes: 4));
    }

    private static async Task AssertUnavailableAsync(Func<Task> action)
    {
        ArgumentNullException.ThrowIfNull(action);

        await action.Should().ThrowAsync<ImageGenerationProviderException>()
            .Where(exception =>
                exception.FailureKind == ImageGenerationProviderFailureKind.Unavailable);
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

    private static async Task<PlaceholderImage> GetNextImageAsync(
        string imagesDirectory,
        long maxImageBytes = TestGenerationOptions.DefaultMaxImageBytes)
    {
        FileSystemPlaceholderImageProvider provider = CreateProvider(
            imagesDirectory,
            maxImageBytes);

        return await provider.GetNextAsync(
                ProviderModelId,
                0,
                CancellationToken.None)
            .ConfigureAwait(false);
    }
}
