using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.Media.Imaging;

using FluentAssertions;
using SkiaSharp;
using Xunit;

using AtomicArt.Desktop.Controls.Generation;
using AtomicArt.Desktop.Tests.Controls.Gallery;

namespace AtomicArt.Desktop.Tests.Controls.Generation;

public sealed class AttachmentPixelLoadingControlTests : AnimatedGalleryControlTestBase
{
    private const int PixelCenterCoordinate = 6;
    private const int RemovalFadeDurationMilliseconds = 160;

    [Fact]
    public void CreatePixelStates_WithSameSeed_ReturnsSameStates()
    {
        Guid seed = Guid.Parse("12345678-1234-1234-1234-123456789abc");

        PixelLoadingState[] first =
            AttachmentPixelLoadingControl.CreatePixelStates(16, seed);
        PixelLoadingState[] second =
            AttachmentPixelLoadingControl.CreatePixelStates(16, seed);

        first.Should().Equal(second);
    }

    [Fact]
    public void Render_With16By16Grid_DrawsColoredPixels()
    {
        Dispatch(() =>
        {
            AttachmentPixelLoadingControl control = new()
            {
                GridSize = 16
            };
            Border host = new()
            {
                Width = 220d,
                Height = 220d,
                Background = Brushes.Black,
                Child = control
            };
            Window window = Show(host, 220d, 220d);

            try
            {
                using SKBitmap bitmap = CaptureRenderedBitmap(window);

                SKColor pixel = bitmap.GetPixel(
                    PixelCenterCoordinate,
                    PixelCenterCoordinate);
                int strongestColorChannel = Math.Max(
                    pixel.Red,
                    Math.Max(pixel.Green, pixel.Blue));

                strongestColorChannel.Should().BeGreaterThan(8);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public async Task FadeOut_WithRemovalDuration_RemainsVisibleUntilRemovalEnds()
    {
        await DispatchAsync(async () =>
        {
            AttachmentPixelLoadingControl control = new()
            {
                AnimationSeed = Guid.Parse("12345678-1234-1234-1234-123456789abc"),
                GridSize = 16
            };
            Border host = new()
            {
                Width = 220d,
                Height = 220d,
                Background = Brushes.Black,
                Child = control
            };
            Window window = Show(host, 220d, 220d);

            try
            {
                control.FadeOut(RemovalFadeDurationMilliseconds);

                control.IsVisible.Should().BeTrue();

                await Task.Delay(RemovalFadeDurationMilliseconds / 2);

                control.IsVisible.Should().BeTrue();

                await Task.Delay(RemovalFadeDurationMilliseconds);

                control.IsVisible.Should().BeFalse();
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public async Task FadeOut_BeforeAttached_RemainsVisibleUntilRemovalEnds()
    {
        await DispatchAsync(async () =>
        {
            AttachmentPixelLoadingControl control = new()
            {
                AnimationSeed = Guid.Parse("12345678-1234-1234-1234-123456789abc"),
                GridSize = 16
            };

            control.FadeOut(RemovalFadeDurationMilliseconds);

            control.IsVisible.Should().BeTrue();

            await Task.Delay(RemovalFadeDurationMilliseconds / 2);

            control.IsVisible.Should().BeTrue();

            await Task.Delay(RemovalFadeDurationMilliseconds);

            control.IsVisible.Should().BeFalse();
        });
    }

    private static SKBitmap CaptureRenderedBitmap(Window window)
    {
        using Bitmap frame = window.CaptureRenderedFrame()
            ?? throw new InvalidOperationException("Rendered frame was not captured.");
        using MemoryStream stream = new();
        frame.Save(stream);
        stream.Position = 0;

        return SKBitmap.Decode(stream)
            ?? throw new InvalidOperationException("Rendered frame could not be decoded.");
    }
}
