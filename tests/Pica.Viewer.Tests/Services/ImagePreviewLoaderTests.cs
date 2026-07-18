using Avalonia;
using Avalonia.Headless;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using SkiaSharp;
using Xunit;

using Pica.Viewer.Services;
using Pica.Protocol;

namespace Pica.Viewer.Tests.Services;

public sealed class ImagePreviewLoaderTests
{
    private static readonly Guid ItemId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly SemaphoreSlim SessionLock = new(1, 1);

    public static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder
            .Configure<Application>()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions());
    }

    [Fact]
    public async Task LoadAsync_WithProvidedPreview_UsesPreviewAndPreservesSourceDimensions()
    {
        await DispatchAsync(async () =>
        {
            string testDirectory = CreateCleanTestDirectory(nameof(
                LoadAsync_WithProvidedPreview_UsesPreviewAndPreservesSourceDimensions));
            string sourcePath = Path.Combine(testDirectory, "source.png");
            string previewPath = Path.Combine(testDirectory, "provided.png");
            CreatePng(sourcePath, 400, 200);
            CreatePng(previewPath, 32, 16);
            ImagePreviewLoader loader = new(NullLogger<ImagePreviewLoader>.Instance);
            PicaImageItem item = new(ItemId, sourcePath, "source.png", previewPath);

            DecodedImagePreview preview = await loader.LoadAsync(item, CancellationToken.None);

            preview.SourcePixelSize.Should().Be(new PixelSize(400, 200));
            preview.Bitmap.Dispose();
        });
    }

    [Fact]
    public async Task LoadAsync_WithoutProvidedPreview_DecodesSmallPreviewWithoutCreatingFiles()
    {
        await DispatchAsync(async () =>
        {
            string testDirectory = CreateCleanTestDirectory(nameof(
                LoadAsync_WithoutProvidedPreview_DecodesSmallPreviewWithoutCreatingFiles));
            string sourcePath = Path.Combine(testDirectory, "source.png");
            CreatePng(sourcePath, 400, 200);
            ImagePreviewLoader loader = new(NullLogger<ImagePreviewLoader>.Instance);
            PicaImageItem item = new(ItemId, sourcePath, "source.png");

            DecodedImagePreview preview = await loader.LoadAsync(item, CancellationToken.None);

            preview.SourcePixelSize.Should().Be(new PixelSize(400, 200));
            ImagePreviewLoader.PreviewDecodeWidth.Should().Be(128);
            Directory.GetFiles(testDirectory).Should().ContainSingle().Which.Should().Be(sourcePath);
            preview.Bitmap.Dispose();
        });
    }

    private static async Task DispatchAsync(Func<Task> action)
    {
        await SessionLock.WaitAsync();

        try
        {
            await using HeadlessUnitTestSession session = HeadlessUnitTestSession.StartNew(
                typeof(ImagePreviewLoaderTests));
            await session.Dispatch(
                async () =>
                {
                    await action();

                    return true;
                },
                CancellationToken.None);
        }
        finally
        {
            SessionLock.Release();
        }
    }

    private static string CreateCleanTestDirectory(string testName)
    {
        string testRoot = Path.Combine(
            Path.GetTempPath(),
            nameof(Pica),
            nameof(ImagePreviewLoaderTests));
        string testDirectory = Path.GetFullPath(Path.Combine(testRoot, testName));

        if (Directory.Exists(testDirectory))
        {
            Directory.Delete(testDirectory, true);
        }

        Directory.CreateDirectory(testDirectory);

        return testDirectory;
    }

    private static void CreatePng(string path, int width, int height)
    {
        using SKBitmap bitmap = new(width, height);
        bitmap.Erase(SKColors.CornflowerBlue);
        using SKImage image = SKImage.FromBitmap(bitmap);
        using SKData data = image.Encode(SKEncodedImageFormat.Png, 100)
            ?? throw new InvalidOperationException("Failed to create the test image.");
        using FileStream stream = new(path, FileMode.Create, FileAccess.Write, FileShare.None);
        data.SaveTo(stream);
    }
}
