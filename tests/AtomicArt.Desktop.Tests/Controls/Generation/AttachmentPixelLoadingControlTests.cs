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
