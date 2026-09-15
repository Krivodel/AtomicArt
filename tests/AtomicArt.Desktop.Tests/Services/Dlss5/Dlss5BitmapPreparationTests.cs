using Avalonia.Media.Imaging;
using FluentAssertions;
using SkiaSharp;
using Xunit;

using AtomicArt.Desktop.Services.Dlss5;
using AtomicArt.Desktop.Tests.Common;

namespace AtomicArt.Desktop.Tests.Services.Dlss5;

public sealed class Dlss5BitmapPreparationTests : DesktopControlTestBase
{
    [Fact]
    public void CreatePixelBuffers_WithKnownRgbaImage_PreservesDimensionsAndPixels()
    {
        using SKBitmap source = new(64, 64, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        source.SetPixel(0, 0, new SKColor(10, 20, 30, 255));
        source.SetPixel(1, 0, new SKColor(40, 50, 60, 255));
        source.SetPixel(2, 0, new SKColor(70, 80, 90, 255));
        source.SetPixel(3, 0, new SKColor(100, 110, 120, 255));

        Dlss5PixelBuffers buffers = Dlss5PixelBuffers.Create(source);

        buffers.Width.Should().Be(64);
        buffers.Height.Should().Be(64);
        buffers.InputRgba.Take(16).Should().Equal(
            [
                10, 20, 30, 255,
                40, 50, 60, 255,
                70, 80, 90, 255,
                100, 110, 120, 255
            ]);
    }

    [Fact]
    public void RestoreSourceAlpha_WhenRendererChangesAlpha_RestoresOriginalAlpha()
    {
        using SKBitmap source = new(64, 64, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        source.SetPixel(0, 0, new SKColor(10, 20, 30, 37));

        Dlss5PixelBuffers buffers = Dlss5PixelBuffers.Create(source);
        buffers.OutputRgba[3] = 255;

        buffers.RestoreSourceAlpha();

        buffers.OutputRgba[3].Should().Be(37);
    }

    [Fact]
    public void CreatePixelBuffers_WithPremultipliedSource_NormalizesToStraightRgba()
    {
        using SKBitmap source = new(64, 64, SKColorType.Rgba8888, SKAlphaType.Premul);
        source.SetPixel(0, 0, new SKColor(128, 64, 32, 128));

        Dlss5PixelBuffers buffers = Dlss5PixelBuffers.Create(source);

        buffers.InputRgba.Take(4).Should().Equal([128, 64, 32, 128]);
    }

    [Fact]
    public async Task CreateAvaloniaBitmap_WhenSourceIsDisposed_ReturnsUsableBitmap()
    {
        await DispatchAsync(() =>
        {
            Bitmap displayBitmap;
            using (SKBitmap source = new(2, 2, SKColorType.Rgba8888, SKAlphaType.Unpremul))
            {
                displayBitmap = Dlss5BitmapPreparation.CreateAvaloniaBitmap(source);
            }

            displayBitmap.PixelSize.Width.Should().BeGreaterThan(0);
            displayBitmap.PixelSize.Height.Should().BeGreaterThan(0);

            displayBitmap.Dispose();

            return Task.CompletedTask;
        });
    }

    [Fact]
    public void SourceSnapshot_WhenSourceIsDisposed_PreservesPixels()
    {
        using SKBitmap source = new(2, 2, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        source.SetPixel(0, 0, new SKColor(10, 20, 30, 255));
        source.SetPixel(1, 0, new SKColor(40, 50, 60, 255));
        source.SetPixel(0, 1, new SKColor(70, 80, 90, 255));
        source.SetPixel(1, 1, new SKColor(100, 110, 120, 255));

        using SKImage snapshot = SKImage.FromBitmap(source);
        source.Dispose();
        using SKBitmap copy = Dlss5BitmapSnapshot.CreateBitmap(snapshot);
        copy.GetPixel(1, 1).Should().Be(new SKColor(100, 110, 120, 255));
    }
}
