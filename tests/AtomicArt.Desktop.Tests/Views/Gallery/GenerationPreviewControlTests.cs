using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;

using FluentAssertions;
using Xunit;

using AtomicArt.Contracts.Generation;
using AtomicArt.Desktop.Controls.Generation;
using AtomicArt.Desktop.Services.Gallery.Thumbnails;
using AtomicArt.Desktop.Tests.Controls.Gallery;
using AtomicArt.Desktop.Tests.Services.Gallery.Thumbnails;
using AtomicArt.Desktop.Tests.Services.Generation;
using AtomicArt.Desktop.ViewModels.Gallery;
using AtomicArt.Desktop.Views.Gallery;
using AtomicArt.Tests.Common.Generation;

namespace AtomicArt.Desktop.Tests.Views.Gallery;

public sealed class GenerationPreviewControlTests : AnimatedGalleryControlTestBase
{
    private const string FirstImagePath = "first.png";
    private const string SecondImagePath = "second.png";

    [Fact]
    public void GenerationProgress_WhenGenerationFails_StopsPixelAnimation()
    {
        Dispatch(() =>
        {
            GenerationItemDto item = GenerationItemDtoTestFactory.Create(
                status: GenerationItemStatus.Generating);
            GenerationItemViewModel viewModel = new(
                item,
                0,
                null,
                GenerationItemStatusDescriptorRegistryTestFactory.Create());
            GenerationPreviewControl control = new()
            {
                DataContext = viewModel
            };

            Show(control, 220d, 220d, window =>
            {
                AttachmentPixelLoadingControl indicator = control
                    .GetVisualDescendants()
                    .OfType<AttachmentPixelLoadingControl>()
                    .Single();

                indicator.AnimationSeed.Should().Be(viewModel.Id);
                indicator.GridSize.Should().Be(16);
                indicator.IsActive.Should().BeTrue();

                viewModel.MarkFailed();
                window.CaptureRenderedFrame();

                indicator.IsActive.Should().BeFalse();
            });
        });
    }

    [Fact]
    public async Task PreviewPath_WhenPreviousLoadCompletesLate_KeepsCurrentBitmap()
    {
        await DispatchAsync(async () =>
        {
            using Bitmap firstBitmap = CreateBitmap();
            using Bitmap secondBitmap = CreateBitmap();
            TaskCompletionSource<GalleryPreviewBitmapLease?> firstCompletion =
                new(TaskCreationOptions.RunContinuationsAsynchronously);
            TaskCompletionSource<GalleryPreviewBitmapLease?> secondCompletion =
                new(TaskCreationOptions.RunContinuationsAsynchronously);
            int firstReleaseCount = 0;
            StubGalleryPreviewBitmapProvider provider = new(
                (imagePath, _) => string.Equals(
                    imagePath,
                    FirstImagePath,
                    StringComparison.Ordinal)
                    ? firstCompletion.Task
                    : secondCompletion.Task);
            TestUiFrameScheduler frameScheduler = new();
            GalleryPreviewSourceScheduler sourceScheduler =
                new(frameScheduler);
            GenerationPreviewControl control = new()
            {
                PreviewPath = FirstImagePath
            };
            control.SetPreviewBitmapServices(provider, sourceScheduler);
            Window window = Show(control);

            try
            {
                Image image = control
                    .GetVisualDescendants()
                    .OfType<Image>()
                    .Single();

                control.PreviewPath = SecondImagePath;
                secondCompletion.SetResult(new GalleryPreviewBitmapLease(
                    secondBitmap,
                    () => { }));
                await Task.Yield();
                frameScheduler.RunNextFrame(TimeSpan.Zero);
                await Task.Yield();
                firstCompletion.SetResult(new GalleryPreviewBitmapLease(
                    firstBitmap,
                    () => firstReleaseCount++));
                await Task.Yield();

                image.Source.Should().BeSameAs(secondBitmap);
                firstReleaseCount.Should().Be(1);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public async Task OnDetachedFromVisualTree_WithLoadedBitmap_ClearsAndReleasesBitmap()
    {
        await DispatchAsync(async () =>
        {
            using Bitmap bitmap = CreateBitmap();
            int releaseCount = 0;
            StubGalleryPreviewBitmapProvider provider = new(
                (_, _) => Task.FromResult<GalleryPreviewBitmapLease?>(
                    new GalleryPreviewBitmapLease(
                        bitmap,
                        () => releaseCount++)));
            TestUiFrameScheduler frameScheduler = new();
            GalleryPreviewSourceScheduler sourceScheduler =
                new(frameScheduler);
            GenerationPreviewControl control = new()
            {
                PreviewPath = FirstImagePath
            };
            control.SetPreviewBitmapServices(provider, sourceScheduler);
            Window window = Show(control);

            try
            {
                Image image = control
                    .GetVisualDescendants()
                    .OfType<Image>()
                    .Single();
                frameScheduler.RunNextFrame(TimeSpan.Zero);
                await Task.Yield();

                window.Close();

                image.Source.Should().BeNull();
                releaseCount.Should().Be(1);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public async Task RemovalTransfer_WithLoadedBitmap_PreservesBitmapUntilFinalDetach()
    {
        await DispatchAsync(async () =>
        {
            using Bitmap bitmap = CreateBitmap();
            int acquireCount = 0;
            int releaseCount = 0;
            StubGalleryPreviewBitmapProvider provider = new(
                (_, _) =>
                {
                    acquireCount++;

                    return Task.FromResult<GalleryPreviewBitmapLease?>(
                        new GalleryPreviewBitmapLease(
                            bitmap,
                            () => releaseCount++));
                });
            TestUiFrameScheduler frameScheduler = new();
            GalleryPreviewSourceScheduler sourceScheduler =
                new(frameScheduler);
            GenerationPreviewControl control = new()
            {
                PreviewPath = FirstImagePath
            };
            control.SetPreviewBitmapServices(provider, sourceScheduler);
            Canvas galleryPanel = new();
            Canvas overlayCanvas = new();
            Grid root = new();
            root.Children.Add(galleryPanel);
            root.Children.Add(overlayCanvas);
            galleryPanel.Children.Add(control);
            Window window = Show(root);

            try
            {
                Image image = control
                    .GetVisualDescendants()
                    .OfType<Image>()
                    .Single();
                frameScheduler.RunNextFrame(TimeSpan.Zero);
                await Task.Yield();

                control.PrepareForRemovalTransfer();
                galleryPanel.Children.Remove(control);
                overlayCanvas.Children.Add(control);
                window.CaptureRenderedFrame();

                image.Source.Should().BeSameAs(bitmap);
                acquireCount.Should().Be(1);
                releaseCount.Should().Be(0);

                overlayCanvas.Children.Remove(control);
                window.CaptureRenderedFrame();

                image.Source.Should().BeNull();
                releaseCount.Should().Be(1);
            }
            finally
            {
                window.Close();
            }
        });
    }

    private static Bitmap CreateBitmap()
    {
        byte[] bytes = GalleryThumbnailTestImages.CreatePngBytes(2, 2);
        using MemoryStream stream = new(bytes);

        return new Bitmap(stream);
    }
}
