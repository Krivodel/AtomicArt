using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Presenters;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;

using FluentAssertions;
using SkiaSharp;
using Xunit;

using AtomicArt.Desktop.Behaviors;
using AtomicArt.Desktop.Tests.Controls.Gallery;

namespace AtomicArt.Desktop.Tests.Behaviors;

public sealed class VerticalFadeMaskBehaviorTests : AnimatedGalleryControlTestBase
{
    private const int CenterX = 50;

    [Fact]
    public void ScrollViewer_WhenScrolled_RendersFadeAtBothViewportEdges()
    {
        Dispatch(() =>
        {
            Border content = new()
            {
                Width = 100d,
                Height = 300d,
                Background = Brushes.White
            };
            ScrollViewer scrollViewer = new()
            {
                Width = 100d,
                Height = 100d,
                Content = content,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                VerticalScrollBarVisibility = ScrollBarVisibility.Hidden
            };

            Border host = new()
            {
                Width = 100d,
                Height = 100d,
                Background = Brushes.Black,
                Child = scrollViewer
            };
            Window window = Show(host, 100d, 100d);

            try
            {
                ScrollContentPresenter scrollPresenter = scrollViewer
                    .GetVisualDescendants()
                    .OfType<ScrollContentPresenter>()
                    .Single();
                VerticalFadeMaskBehavior.SetInsets(
                    scrollPresenter,
                    new Thickness(0d, 20d, 0d, 20d));
                scrollPresenter.Offset = new Vector(0d, 100d);
                using SKBitmap bitmap = CaptureRenderedBitmap(window);

                byte topEdge = bitmap.GetPixel(CenterX, 0).Red;
                byte topFade = bitmap.GetPixel(CenterX, 10).Red;
                byte center = bitmap.GetPixel(CenterX, 50).Red;
                byte bottomFade = bitmap.GetPixel(CenterX, 89).Red;
                byte bottomEdge = bitmap.GetPixel(CenterX, 99).Red;

                topEdge.Should().BeLessThan(topFade);
                topFade.Should().BeLessThan(center);
                bottomEdge.Should().BeLessThan(bottomFade);
                bottomFade.Should().BeLessThan(center);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void TextBox_WhenScrolled_RendersFadeOnClippedText()
    {
        Dispatch(() =>
        {
            TextBox textBox = new()
            {
                AcceptsReturn = true,
                Width = 200d,
                Height = 80d,
                Background = Brushes.Black,
                BorderThickness = new Thickness(0d),
                FontSize = 20d,
                Foreground = Brushes.White,
                LineHeight = 24d,
                Text = string.Join(
                    Environment.NewLine,
                    Enumerable.Range(1, 20).Select(lineNumber => $"Line {lineNumber}"))
            };
            Window window = Show(textBox, 200d, 80d);

            try
            {
                ScrollContentPresenter scrollPresenter = textBox
                    .GetVisualDescendants()
                    .OfType<ScrollContentPresenter>()
                    .Single();

                scrollPresenter.Offset = new Vector(0d, 12d);
                using SKBitmap fadedBitmap = CaptureRenderedBitmap(window);
                int fadeHeight = Math.Max(
                    1,
                    (int)Math.Ceiling(
                        VerticalFadeMaskBehavior.GetInsets(scrollPresenter).Top));

                scrollPresenter.OpacityMask = null;
                using SKBitmap unmaskedBitmap = CaptureRenderedBitmap(window);

                long fadedBrightness = GetBrightness(
                    fadedBitmap,
                    fadeHeight);
                long unmaskedBrightness = GetBrightness(
                    unmaskedBitmap,
                    fadeHeight);

                fadedBrightness.Should().BeLessThan(unmaskedBrightness);
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

    private static long GetBrightness(SKBitmap bitmap, int height)
    {
        long brightness = 0L;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < bitmap.Width; x++)
            {
                SKColor pixel = bitmap.GetPixel(x, y);
                brightness += pixel.Red + pixel.Green + pixel.Blue;
            }
        }

        return brightness;
    }
}
