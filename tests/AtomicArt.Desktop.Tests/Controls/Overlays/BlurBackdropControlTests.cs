using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.Media.Imaging;

using FluentAssertions;
using SkiaSharp;
using Xunit;

using AtomicArt.Desktop.Controls.Overlays;
using AtomicArt.Desktop.Tests.Controls.Gallery;

namespace AtomicArt.Desktop.Tests.Controls.Overlays;

public sealed class BlurBackdropControlTests : AnimatedGalleryControlTestBase
{
    private const int BlurredEdgeX = 22;
    private const int BlurredStripeX = 100;
    private const int CenterY = 50;
    private const int OutsideBlurY = 5;

    [Fact]
    public void Render_WithHighContrastBackdrop_BlursCenterWithoutDarkeningEdges()
    {
        Dispatch(() =>
        {
            Canvas root = CreateBackdrop();
            Window window = Show(root, 200d, 100d);

            try
            {
                using SKBitmap bitmap = CaptureRenderedBitmap(window);

                SKColor outsideStripe = bitmap.GetPixel(BlurredStripeX, OutsideBlurY);
                SKColor blurredStripe = bitmap.GetPixel(BlurredStripeX, CenterY);
                SKColor blurredEdge = bitmap.GetPixel(BlurredEdgeX, CenterY);

                outsideStripe.Red.Should().BeLessThan((byte)10);
                blurredStripe.Red.Should().BeInRange((byte)30, (byte)245);
                blurredStripe.Green.Should().Be(blurredStripe.Red);
                blurredStripe.Blue.Should().Be(blurredStripe.Red);
                blurredEdge.Red.Should().BeGreaterThan((byte)245);
                blurredEdge.Green.Should().BeGreaterThan((byte)245);
                blurredEdge.Blue.Should().BeGreaterThan((byte)245);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void Render_WithZeroIntensity_LeavesBackdropUnblurred()
    {
        Dispatch(() =>
        {
            Canvas root = CreateBackdrop(0d);
            Window window = Show(root, 200d, 100d);

            try
            {
                using SKBitmap bitmap = CaptureRenderedBitmap(window);

                SKColor stripe = bitmap.GetPixel(BlurredStripeX, CenterY);

                stripe.Red.Should().BeLessThan((byte)10);
                stripe.Green.Should().BeLessThan((byte)10);
                stripe.Blue.Should().BeLessThan((byte)10);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void Render_WithLargeBlurRadius_BlursDownsampledBackdropAcrossBounds()
    {
        Dispatch(() =>
        {
            Canvas root = CreateBackdrop(1d, 120d);
            Window window = Show(root, 200d, 100d);

            try
            {
                using SKBitmap bitmap = CaptureRenderedBitmap(window);

                SKColor outsideStripe = bitmap.GetPixel(BlurredStripeX, OutsideBlurY);
                SKColor blurredStripe = bitmap.GetPixel(BlurredStripeX, CenterY);
                SKColor blurredEdge = bitmap.GetPixel(BlurredEdgeX, CenterY);

                outsideStripe.Red.Should().BeLessThan((byte)10);
                blurredStripe.Red.Should().BeInRange((byte)200, (byte)254);
                blurredStripe.Green.Should().Be(blurredStripe.Red);
                blurredStripe.Blue.Should().Be(blurredStripe.Red);
                blurredEdge.Red.Should().BeGreaterThan((byte)245);
                blurredEdge.Green.Should().BeGreaterThan((byte)245);
                blurredEdge.Blue.Should().BeGreaterThan((byte)245);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void Render_AcrossDownsamplingThreshold_KeepsBlurStrengthContinuous()
    {
        Dispatch(() =>
        {
            Canvas directRoot = CreateBackdrop(1d, 35.97d);
            Canvas downsampledRoot = CreateBackdrop(1d, 36.03d);
            Window directWindow = Show(directRoot, 200d, 100d);
            Window downsampledWindow = Show(downsampledRoot, 200d, 100d);

            try
            {
                using SKBitmap directBitmap =
                    CaptureRenderedBitmap(directWindow);
                using SKBitmap downsampledBitmap =
                    CaptureRenderedBitmap(downsampledWindow);
                byte directBrightness =
                    directBitmap.GetPixel(BlurredStripeX, CenterY).Red;
                byte downsampledBrightness =
                    downsampledBitmap.GetPixel(BlurredStripeX, CenterY).Red;

                Math.Abs(directBrightness - downsampledBrightness)
                    .Should()
                    .BeLessThanOrEqualTo(3);
            }
            finally
            {
                directWindow.Close();
                downsampledWindow.Close();
            }
        });
    }

    [Fact]
    public void Render_WithLargeBlurRadius_BlursBackdropAtControlEdge()
    {
        Dispatch(() =>
        {
            Canvas root = CreateEdgeBackdrop();
            Window window = Show(root, 200d, 100d);

            try
            {
                using SKBitmap bitmap = CaptureRenderedBitmap(window);

                SKColor outsideEdge = bitmap.GetPixel(BlurredEdgeX, OutsideBlurY);
                SKColor blurredEdge = bitmap.GetPixel(BlurredEdgeX, CenterY);

                outsideEdge.Red.Should().BeLessThan((byte)10);
                blurredEdge.Red.Should().BeGreaterThan((byte)150);
                blurredEdge.Green.Should().Be(blurredEdge.Red);
                blurredEdge.Blue.Should().Be(blurredEdge.Red);
            }
            finally
            {
                window.Close();
            }
        });
    }

    private static Canvas CreateBackdrop()
    {
        return CreateBackdrop(1d);
    }

    private static Canvas CreateBackdrop(double blurIntensity)
    {
        return CreateBackdrop(blurIntensity, 18d);
    }

    private static Canvas CreateBackdrop(
        double blurIntensity,
        double blurRadius)
    {
        Canvas root = new()
        {
            Background = Brushes.White
        };
        Border stripe = new()
        {
            Width = 4d,
            Height = 100d,
            Background = Brushes.Black
        };
        BlurBackdropControl blurBackdrop = new()
        {
            Width = 160d,
            Height = 80d,
            BlurRadius = blurRadius,
            Intensity = blurIntensity
        };

        Canvas.SetLeft(stripe, 98d);
        Canvas.SetTop(stripe, 0d);
        Canvas.SetLeft(blurBackdrop, 20d);
        Canvas.SetTop(blurBackdrop, 10d);
        root.Children.Add(stripe);
        root.Children.Add(blurBackdrop);

        return root;
    }

    private static Canvas CreateEdgeBackdrop()
    {
        Canvas root = new()
        {
            Background = Brushes.White
        };
        Border edgeStripe = new()
        {
            Width = 20d,
            Height = 100d,
            Background = Brushes.Black
        };
        BlurBackdropControl blurBackdrop = new()
        {
            Width = 160d,
            Height = 80d,
            BlurRadius = 120d
        };

        Canvas.SetLeft(edgeStripe, 20d);
        Canvas.SetTop(edgeStripe, 0d);
        Canvas.SetLeft(blurBackdrop, 20d);
        Canvas.SetTop(blurBackdrop, 10d);
        root.Children.Add(edgeStripe);
        root.Children.Add(blurBackdrop);

        return root;
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
