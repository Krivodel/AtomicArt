using Avalonia;
using Avalonia.Headless;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using SkiaSharp;
using Xunit;

using AtomicArt.Tests.Avalonia;
using Pica.Protocol;
using Pica.Tests.Common;
using Pica.Viewer.Services;

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
            using PicaTemporaryDirectory temporaryDirectory = new();
            string sourcePath = Path.Combine(temporaryDirectory.DirectoryPath, "source.png");
            string previewPath = Path.Combine(temporaryDirectory.DirectoryPath, "provided.png");
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
            using PicaTemporaryDirectory temporaryDirectory = new();
            string sourcePath = Path.Combine(temporaryDirectory.DirectoryPath, "source.png");
            CreatePng(sourcePath, 400, 200);
            ImagePreviewLoader loader = new(NullLogger<ImagePreviewLoader>.Instance);
            PicaImageItem item = new(ItemId, sourcePath, "source.png");

            DecodedImagePreview preview = await loader.LoadAsync(item, CancellationToken.None);

            preview.SourcePixelSize.Should().Be(new PixelSize(400, 200));
            ImagePreviewLoader.PreviewDecodeWidth.Should().Be(128);
            Directory.GetFiles(temporaryDirectory.DirectoryPath)
                .Should()
                .ContainSingle()
                .Which
                .Should()
                .Be(sourcePath);
            preview.Bitmap.Dispose();
        });
    }

    private static async Task DispatchAsync(Func<Task> action)
    {
        await HeadlessTestSessionDispatcher.DispatchAsync(
            typeof(ImagePreviewLoaderTests),
            SessionLock,
            action);
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
