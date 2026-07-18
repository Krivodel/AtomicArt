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
    private const int SourceWidth = 400;
    private const int SourceHeight = 200;
    private const string SourceFileName = "source.png";

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
            using ImagePreviewTestContext context = new();
            context.AddProvidedPreview(32, 16);

            DecodedImagePreview preview = await context.LoadAsync(CancellationToken.None);

            AssertSourcePixelSize(preview);
            preview.Bitmap.Dispose();
        });
    }

    [Fact]
    public async Task LoadAsync_WithoutProvidedPreview_DecodesSmallPreviewWithoutCreatingFiles()
    {
        await DispatchAsync(async () =>
        {
            using ImagePreviewTestContext context = new();

            DecodedImagePreview preview = await context.LoadAsync(CancellationToken.None);

            AssertSourcePixelSize(preview);
            ImagePreviewLoader.PreviewDecodeWidth.Should().Be(128);
            Directory.GetFiles(context.DirectoryPath)
                .Should()
                .ContainSingle()
                .Which
                .Should()
                .Be(context.SourcePath);
            preview.Bitmap.Dispose();
        });
    }

    private static void AssertSourcePixelSize(DecodedImagePreview preview)
    {
        preview.SourcePixelSize.Should().Be(new PixelSize(SourceWidth, SourceHeight));
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

    private sealed class ImagePreviewTestContext : IDisposable
    {
        public string DirectoryPath => _temporaryDirectory.DirectoryPath;
        public string SourcePath { get; }

        private readonly PicaTemporaryDirectory _temporaryDirectory;
        private readonly ImagePreviewLoader _loader;
        private string? _previewPath;

        public ImagePreviewTestContext()
        {
            _temporaryDirectory = new PicaTemporaryDirectory();
            _loader = new ImagePreviewLoader(NullLogger<ImagePreviewLoader>.Instance);
            SourcePath = Path.Combine(DirectoryPath, SourceFileName);
            CreatePng(SourcePath, SourceWidth, SourceHeight);
        }

        public void AddProvidedPreview(int width, int height)
        {
            _previewPath = Path.Combine(DirectoryPath, "provided.png");
            CreatePng(_previewPath, width, height);
        }

        public async Task<DecodedImagePreview> LoadAsync(CancellationToken ct)
        {
            PicaImageItem item = _previewPath is null
                ? new PicaImageItem(ItemId, SourcePath, SourceFileName)
                : new PicaImageItem(ItemId, SourcePath, SourceFileName, _previewPath);

            return await _loader.LoadAsync(item, ct).ConfigureAwait(false);
        }

        public void Dispose()
        {
            _temporaryDirectory.Dispose();
        }
    }
}
