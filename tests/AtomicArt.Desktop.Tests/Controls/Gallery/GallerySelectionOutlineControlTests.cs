using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.Media.Imaging;

using FluentAssertions;
using SkiaSharp;
using Xunit;

using AtomicArt.Desktop.Controls.Gallery;
using AtomicArt.Desktop.Services.UiAnimation;

namespace AtomicArt.Desktop.Tests.Controls.Gallery;

public sealed class GallerySelectionOutlineControlTests : AnimatedGalleryControlTestBase
{
    private const int BackgroundPixelCoordinate = 5;
    private const int OutlineLeadingPixelCoordinate = 11;
    private const int OutlineTrailingPixelCoordinate = 34;
    private const int SurfaceInsidePixelCoordinate = 14;
    private const int SurfaceCenterCoordinate = 23;

    [Fact]
    public void CalculateStrokeCenterBounds_WithPositiveThickness_PlacesStrokeOutsideSurface()
    {
        Size surfaceSize = new(220d, 322d);
        double strokeThickness = 3d;
        Size outlineSize = new(
            surfaceSize.Width + (strokeThickness * 2d),
            surfaceSize.Height + (strokeThickness * 2d));

        Rect strokeCenterBounds = GallerySelectionOutlineControl
            .CalculateStrokeCenterBounds(outlineSize, strokeThickness);

        double halfStrokeThickness = strokeThickness / 2d;
        (strokeCenterBounds.Left - halfStrokeThickness).Should().Be(0d);
        (strokeCenterBounds.Top - halfStrokeThickness).Should().Be(0d);
        (strokeCenterBounds.Right + halfStrokeThickness).Should().Be(
            outlineSize.Width);
        (strokeCenterBounds.Bottom + halfStrokeThickness).Should().Be(
            outlineSize.Height);
        (strokeCenterBounds.Left + halfStrokeThickness).Should().Be(strokeThickness);
        (strokeCenterBounds.Top + halfStrokeThickness).Should().Be(strokeThickness);
        (strokeCenterBounds.Right - halfStrokeThickness).Should().Be(
            surfaceSize.Width + strokeThickness);
        (strokeCenterBounds.Bottom - halfStrokeThickness).Should().Be(
            surfaceSize.Height + strokeThickness);
    }

    [Fact]
    public void Render_WithSquareOutline_DrawsFullThicknessOutsideSurface()
    {
        Dispatch(() =>
        {
            GallerySelectionOutlineControl outline = new()
            {
                ClipToBounds = false,
                Effect = new DropShadowEffect
                {
                    BlurRadius = 8d,
                    Color = Colors.Lime,
                    Opacity = 0.5d
                },
                Stroke = Brushes.Lime,
                StrokeThickness = 3d
            };
            Border surface = new()
            {
                Background = Brushes.White
            };
            GallerySelectionCardPanel card = new()
            {
                Width = 20d,
                Height = 20d,
                ClipToBounds = false,
                OutlineThickness = 3d
            };
            card.Children.Add(surface);
            card.Children.Add(outline);
            Border cardContainer = new()
            {
                Margin = new Thickness(0d, 0d, 16d, 16d),
                ClipToBounds = false,
                CornerRadius = new CornerRadius(8d),
                Child = card
            };
            UserControl cardControl = new()
            {
                Width = 36d,
                Height = 36d,
                Content = cardContainer
            };
            MotionFrameApplier.Apply(cardControl, MotionFrame.Identity);
            Canvas root = new()
            {
                Width = 50d,
                Height = 50d,
                Background = Brushes.Black
            };
            Canvas.SetLeft(cardControl, 10d);
            Canvas.SetTop(cardControl, 10d);
            root.Children.Add(cardControl);
            Window window = Show(root, 50d, 50d);

            try
            {
                using SKBitmap bitmap = CaptureRenderedBitmap(window);

                SKColor backgroundPixel = bitmap.GetPixel(
                    BackgroundPixelCoordinate,
                    SurfaceCenterCoordinate);
                SKColor outsideLeftPixel = bitmap.GetPixel(
                    OutlineLeadingPixelCoordinate,
                    SurfaceCenterCoordinate);
                SKColor outsideTopPixel = bitmap.GetPixel(
                    SurfaceCenterCoordinate,
                    OutlineLeadingPixelCoordinate);
                SKColor outsideRightPixel = bitmap.GetPixel(
                    OutlineTrailingPixelCoordinate,
                    SurfaceCenterCoordinate);
                SKColor outsideBottomPixel = bitmap.GetPixel(
                    SurfaceCenterCoordinate,
                    OutlineTrailingPixelCoordinate);
                SKColor outsideCornerPixel = bitmap.GetPixel(
                    OutlineLeadingPixelCoordinate,
                    OutlineLeadingPixelCoordinate);
                SKColor surfacePixel = bitmap.GetPixel(
                    SurfaceInsidePixelCoordinate,
                    SurfaceCenterCoordinate);

                backgroundPixel.Red.Should().BeLessThan((byte)10);
                backgroundPixel.Green.Should().BeLessThan((byte)10);
                backgroundPixel.Blue.Should().BeLessThan((byte)10);
                outsideLeftPixel.Red.Should().BeLessThan((byte)10);
                outsideLeftPixel.Green.Should().BeGreaterThan((byte)245);
                outsideLeftPixel.Blue.Should().BeLessThan((byte)10);
                outsideTopPixel.Red.Should().BeLessThan((byte)10);
                outsideTopPixel.Green.Should().BeGreaterThan((byte)245);
                outsideTopPixel.Blue.Should().BeLessThan((byte)10);
                outsideRightPixel.Red.Should().BeLessThan((byte)10);
                outsideRightPixel.Green.Should().BeGreaterThan((byte)245);
                outsideRightPixel.Blue.Should().BeLessThan((byte)10);
                outsideBottomPixel.Red.Should().BeLessThan((byte)10);
                outsideBottomPixel.Green.Should().BeGreaterThan((byte)245);
                outsideBottomPixel.Blue.Should().BeLessThan((byte)10);
                outsideCornerPixel.Red.Should().BeLessThan((byte)10);
                outsideCornerPixel.Green.Should().BeGreaterThan((byte)245);
                outsideCornerPixel.Blue.Should().BeLessThan((byte)10);
                surfacePixel.Red.Should().BeGreaterThan((byte)245);
                surfacePixel.Green.Should().BeGreaterThan((byte)245);
                surfacePixel.Blue.Should().BeGreaterThan((byte)245);
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
