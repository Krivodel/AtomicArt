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
    public async Task OpenNextAsync_WithSupportedImage_ReturnsFileBackedStream()
    {
        using TemporaryDirectory directory = new(
            typeof(FileSystemPlaceholderImageProviderTests),
            nameof(OpenNextAsync_WithSupportedImage_ReturnsFileBackedStream));
        string path = Path.Combine(directory.DirectoryPath, "streamed-image");
        await File.WriteAllBytesAsync(
            path,
            GenerationImageTestData.MinimalPngBytes,
            CancellationToken.None);
        FileSystemPlaceholderImageProvider provider = CreateProvider(
            directory.DirectoryPath);

        await using StreamingPlaceholderImage image =
            await provider.OpenNextAsync(
                ProviderModelId,
                0,
                CancellationToken.None);

        image.ContentType.Should().Be(GenerationImageContentTypes.Png);
        image.ContentLength.Should().Be(
            GenerationImageTestData.MinimalPngBytes.LongLength);
        image.Content.Should().BeOfType<FileStream>();
    }

    [Fact]
    public async Task OpenNextAsync_WithUnsupportedFiles_SkipsThem()
    {
        using TemporaryDirectory directory = new(
            typeof(FileSystemPlaceholderImageProviderTests),
            nameof(OpenNextAsync_WithUnsupportedFiles_SkipsThem));
        await File.WriteAllTextAsync(
            Path.Combine(directory.DirectoryPath, "notes.txt"),
            "not an image",
            CancellationToken.None);
        await File.WriteAllBytesAsync(
            Path.Combine(directory.DirectoryPath, "image.bin"),
            GenerationImageTestData.MinimalPngBytes,
            CancellationToken.None);
        FileSystemPlaceholderImageProvider provider = CreateProvider(
            directory.DirectoryPath);

        await using StreamingPlaceholderImage image =
            await provider.OpenNextAsync(
                ProviderModelId,
                0,
                CancellationToken.None);

        image.ContentType.Should().Be(GenerationImageContentTypes.Png);
    }

    [Fact]
    public async Task OpenNextAsync_WithEmptyDirectory_ThrowsProviderException()
    {
        using TemporaryDirectory directory = new(
            typeof(FileSystemPlaceholderImageProviderTests),
            nameof(OpenNextAsync_WithEmptyDirectory_ThrowsProviderException));
        FileSystemPlaceholderImageProvider provider = CreateProvider(
            directory.DirectoryPath);

        Func<Task> act = async () =>
        {
            await using StreamingPlaceholderImage image =
                await provider.OpenNextAsync(
                    ProviderModelId,
                    0,
                    CancellationToken.None);
        };

        await act.Should().ThrowAsync<ImageGenerationProviderException>()
            .Where(exception =>
                exception.FailureKind
                    == ImageGenerationProviderFailureKind.Unavailable);
    }

    [Fact]
    public async Task OpenNextAsync_WithImageOverLimit_SkipsFile()
    {
        using TemporaryDirectory directory = new(
            typeof(FileSystemPlaceholderImageProviderTests),
            nameof(OpenNextAsync_WithImageOverLimit_SkipsFile));
        await File.WriteAllBytesAsync(
            Path.Combine(directory.DirectoryPath, "large"),
            GenerationImageTestData.MinimalPngBytes,
            CancellationToken.None);
        FileSystemPlaceholderImageProvider provider = CreateProvider(
            directory.DirectoryPath,
            maxImageBytes: 4);

        Func<Task> act = async () =>
        {
            await using StreamingPlaceholderImage image =
                await provider.OpenNextAsync(
                    ProviderModelId,
                    0,
                    CancellationToken.None);
        };

        await act.Should().ThrowAsync<ImageGenerationProviderException>()
            .Where(exception =>
                exception.FailureKind
                    == ImageGenerationProviderFailureKind.Unavailable);
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
