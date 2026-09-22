using Avalonia.Controls;
using Avalonia.Media;

using FluentAssertions;
using SkiaSharp;
using Xunit;

using AtomicArt.Desktop.Controls;
using AtomicArt.Desktop.Tests.Common;

namespace AtomicArt.Desktop.Tests.Controls;

public sealed class Dlss5ProgressLineTests : DesktopControlTestBase
{
    [Fact]
    public void Render_WithWideTrack_DrawsHighlightedCellsOverHostBackground()
    {
        DesktopControlTestBase.Dispatch(() =>
        {
            Border host = Dlss5ProgressLineTests.CreateProgressHost(240d, 10d);
            Window window = DesktopControlTestBase.Show(host, 240d, 10d);

            try
            {
                using SKBitmap bitmap = CaptureRenderedBitmap(window);

                SKColor highlighted = bitmap.GetPixel(12, 5);
                SKColor dim = bitmap.GetPixel(88, 5);
                SKColor gap = bitmap.GetPixel(21, 5);
                highlighted.Green.Should().BeGreaterThan(dim.Green);
                dim.Green.Should().BeGreaterThan(gap.Green);
                gap.Red.Should().BeGreaterThan(200);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void Render_WithNarrowShortTrack_KeepsCellInsideTrack()
    {
        DesktopControlTestBase.Dispatch(() =>
        {
            Border host = Dlss5ProgressLineTests.CreateProgressHost(20d, 2d);
            Window window = DesktopControlTestBase.Show(host, 20d, 2d);

            try
            {
                using SKBitmap bitmap = CaptureRenderedBitmap(window);

                bitmap.GetPixel(10, 1).Green.Should().BeGreaterThan(0);
                bitmap.GetPixel(3, 1).Red.Should().BeGreaterThan(200);
            }
            finally
            {
                window.Close();
            }
        });
    }

    private static Border CreateProgressHost(double width, double height)
    {
        Dlss5ProgressLine progress = new()
        {
            Width = width,
            Height = height,
            BaseBrush = Brushes.Green,
            HighlightBrush = Brushes.White
        };

        return new Border
        {
            Background = Brushes.Magenta,
            Child = progress
        };
    }
}
