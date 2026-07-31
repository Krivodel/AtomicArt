using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;

using FluentAssertions;
using Xunit;

using AtomicArt.Desktop.Controls;
using AtomicArt.Desktop.Tests.Common;
using AtomicArt.Desktop.Tests.Services.Gallery.Thumbnails;

namespace AtomicArt.Desktop.Tests.Controls;

public sealed class ImageDragPreviewWindowTests : DesktopControlTestBase
{
    [Fact]
    public void Dispose_WithBorrowedBitmap_LeavesBitmapUsable()
    {
        Dispatch(() =>
        {
            using Bitmap bitmap = CreateBitmap();
            using ImageDragPreviewWindow window =
                ImageDragPreviewWindow.CreateBorrowed(bitmap);

            window.Dispose();

            using MemoryStream output = new();
            bitmap.Save(output);
            output.Length.Should().BePositive();
        });
    }

    [Fact]
    public void Start_DuringAppearanceAnimation_ScalesContentWithoutResizingWindow()
    {
        Dispatch(() =>
        {
            using Bitmap bitmap = CreateBitmap();
            TestUiFrameScheduler frameScheduler = new();
            using ImageDragPreviewWindow window =
                ImageDragPreviewWindow.CreateBorrowed(bitmap, frameScheduler);
            Border previewContent = window.Content.Should().BeOfType<Border>().Subject;
            ScaleTransform previewScale = GetScaleTransform(previewContent);
            double initialWidth = window.Width;
            double initialHeight = window.Height;
            double initialOpacity = previewContent.Opacity;

            previewScale.ScaleX.Should().Be(0d);
            previewScale.ScaleY.Should().Be(0d);

            window.Start(null);
            frameScheduler.RunNextFrame(TimeSpan.Zero);
            frameScheduler.RunNextFrame(TimeSpan.FromMilliseconds(1d));

            previewScale.ScaleX.Should().BeGreaterThan(0d).And.BeLessThan(1d);
            previewScale.ScaleY.Should().Be(previewScale.ScaleX);
            window.Width.Should().Be(initialWidth);
            window.Height.Should().Be(initialHeight);
            previewContent.Opacity.Should().Be(initialOpacity);

            frameScheduler.RunNextFrame(TimeSpan.FromSeconds(1d));

            previewScale.ScaleX.Should().Be(1d);
            previewScale.ScaleY.Should().Be(1d);
            window.Width.Should().Be(initialWidth);
            window.Height.Should().Be(initialHeight);
            previewContent.Opacity.Should().Be(initialOpacity);
        });
    }

    [Fact]
    public async Task FinishAsync_DuringDisappearanceAnimation_ScalesContentWithoutResizingWindow()
    {
        await DispatchAsync(async () =>
        {
            using Bitmap bitmap = CreateBitmap();
            TestUiFrameScheduler frameScheduler = new();
            using ImageDragPreviewWindow window =
                ImageDragPreviewWindow.CreateBorrowed(bitmap, frameScheduler);
            Border previewContent = window.Content.Should().BeOfType<Border>().Subject;
            ScaleTransform previewScale = GetScaleTransform(previewContent);
            double initialWidth = window.Width;
            double initialHeight = window.Height;
            double initialOpacity = previewContent.Opacity;
            window.Start(null);
            frameScheduler.RunNextFrame(TimeSpan.Zero);
            await frameScheduler.RunNextFrameAsync(TimeSpan.FromSeconds(1d));

            Task finishTask = window.FinishAsync();
            frameScheduler.RunNextFrame(TimeSpan.FromSeconds(2d));
            await frameScheduler.RunNextFrameAsync(
                TimeSpan.FromMilliseconds(2001d));

            previewScale.ScaleX.Should().BeGreaterThan(0d).And.BeLessThan(1d);
            previewScale.ScaleY.Should().Be(previewScale.ScaleX);
            window.Width.Should().Be(initialWidth);
            window.Height.Should().Be(initialHeight);
            previewContent.Opacity.Should().Be(initialOpacity);

            await frameScheduler.RunNextFrameAsync(TimeSpan.FromSeconds(3d));
            await finishTask;

            previewScale.ScaleX.Should().Be(0d);
            previewScale.ScaleY.Should().Be(0d);
            window.Width.Should().Be(initialWidth);
            window.Height.Should().Be(initialHeight);
            previewContent.Opacity.Should().Be(initialOpacity);
        });
    }

    private static Bitmap CreateBitmap()
    {
        byte[] bytes = GalleryThumbnailTestImages.CreatePngBytes(2, 2);
        using MemoryStream stream = new(bytes);

        return new Bitmap(stream);
    }

    private static ScaleTransform GetScaleTransform(Control control)
    {
        TransformGroup transformGroup = control.RenderTransform
            .Should()
            .BeOfType<TransformGroup>()
            .Subject;

        return transformGroup.Children
            .OfType<ScaleTransform>()
            .Single();
    }
}
