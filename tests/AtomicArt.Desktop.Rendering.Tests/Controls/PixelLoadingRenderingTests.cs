using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using FluentAssertions;
using SkiaSharp;
using Xunit;

using AtomicArt.Contracts.Generation;
using AtomicArt.Desktop.Controls.Generation;
using AtomicArt.Desktop.Rendering.Tests.ViewModels.Generation;
using AtomicArt.Desktop.Tests.Common;
using AtomicArt.Desktop.Tests.Services.Generation;
using AtomicArt.Desktop.ViewModels.Gallery;
using AtomicArt.Desktop.Views.Gallery;
using AtomicArt.Tests.Common.Generation;

namespace AtomicArt.Desktop.Rendering.Tests.Controls;

public sealed class PixelLoadingRenderingTests : DesktopControlTestBase
{
    public new static AppBuilder BuildAvaloniaApp()
    {
        return GenerationChatViewModelTests.BuildAvaloniaApp();
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task Render_WithAncestorOpacity_FadesTheEntireAnimation(bool cacheFrames, bool cacheAncestor)
    {
        await DispatchAsync(typeof(PixelLoadingRenderingTests), () =>
        {
            AttachmentPixelLoadingControl indicator = new()
            {
                AnimationSeed = Guid.Parse("12345678-1234-1234-1234-123456789abc"),
                GridSize = 16,
                CacheFrames = cacheFrames
            };
            Border host = new()
            {
                Child = indicator,
                Opacity = 0.25d,
                CacheMode = cacheAncestor ? new BitmapCache() : null
            };
            Grid surface = new() { Width = 220d, Height = 220d, Background = Brushes.Black, Children = { host } };
            Window window = Show(surface, 220d, 220d);

            try
            {
                using SKBitmap faded = CaptureRenderedBitmap(window);
                host.Opacity = 1d;
                using SKBitmap opaque = CaptureRenderedBitmap(window);
                long fadedBrightness = 0L;
                long opaqueBrightness = 0L;

                for (int y = 0; y < faded.Height; y++)
                {
                    for (int x = 0; x < faded.Width; x++)
                    {
                        SKColor before = faded.GetPixel(x, y);
                        SKColor after = opaque.GetPixel(x, y);
                        fadedBrightness += before.Red + before.Green + before.Blue;
                        opaqueBrightness += after.Red + after.Green + after.Blue;
                    }
                }

                opaqueBrightness.Should().BeGreaterThan(0L);
                ((double)fadedBrightness / opaqueBrightness).Should().BeApproximately(0.25d, 0.06d);
            }
            finally
            {
                window.Close();
            }

            return Task.CompletedTask;
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Render_WhileGenerating_ChangesPixelColorsBetweenFrames(bool cacheFrames)
    {
        await DispatchAsync(typeof(PixelLoadingRenderingTests), async () =>
        {
            AttachmentPixelLoadingControl indicator = new()
            {
                AnimationSeed = Guid.Parse("12345678-1234-1234-1234-123456789abc"),
                GridSize = 16,
                CacheFrames = cacheFrames
            };
            Border host = new() { Width = 220d, Height = 220d, Background = Brushes.Black, Child = indicator };
            Window window = Show(host, 220d, 220d);

            try
            {
                using SKBitmap before = CaptureRenderedBitmap(window);
                await Task.Delay(160);
                using SKBitmap after = CaptureRenderedBitmap(window);

                CountChangedPixels(before, after).Should().BeGreaterThan(100);
                indicator.IsActive.Should().BeTrue();
                indicator.IsVisible.Should().BeTrue();
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public async Task GalleryPreview_WhileGenerating_KeepsAnimatingAfterBeingHidden()
    {
        await DispatchAsync(typeof(PixelLoadingRenderingTests), async () =>
        {
            GenerationItemViewModel card = new(GenerationItemDtoTestFactory.Create(status: GenerationItemStatus.Generating),
                0, null, GenerationItemStatusDescriptorRegistryTestFactory.Create(),
                AtomicArt.Desktop.Tests.TestDoubles.TestLocalizationTextProvider.Default);
            GenerationPreviewControl preview = new() { DataContext = card };
            Border host = new() { Width = 220d, Height = 220d, Background = Brushes.Black, Child = preview };
            Window window = Show(host, 220d, 220d);

            try
            {
                host.IsVisible = false;
                await Task.Delay(80);
                host.IsVisible = true;
                using SKBitmap before = CaptureRenderedBitmap(window);
                await Task.Delay(160);
                using SKBitmap after = CaptureRenderedBitmap(window);

                CountChangedPixels(before, after).Should().BeGreaterThan(100);
            }
            finally
            {
                window.Close();
            }
        });
    }

    internal static int CountChangedPixels(SKBitmap before, SKBitmap after)
    {
        int count = 0;

        for (int y = 0; y < before.Height; y += 4)
        {
            for (int x = 0; x < before.Width; x += 4)
            {
                if (before.GetPixel(x, y) != after.GetPixel(x, y))
                {
                    count++;
                }
            }
        }

        return count;
    }
}
