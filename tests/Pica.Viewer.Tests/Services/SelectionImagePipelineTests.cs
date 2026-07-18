using System.Runtime.InteropServices;

using Avalonia;
using Avalonia.Platform;
using FluentAssertions;
using SkiaSharp;
using Xunit;

using Pica.Viewer.Services;

namespace Pica.Viewer.Tests.Services;

public sealed class SelectionImagePipelineTests
{
    [Fact]
    public void NormalizeSourceRect_WithEmptyRect_ReturnsNull()
    {
        PixelSize sourceSize = new(100, 50);
        PixelRect sourceRect = new();

        PixelRect? normalizedRect = BitmapPixelCopy.NormalizeSourceRect(sourceSize, sourceRect);

        normalizedRect.Should().BeNull();
    }

    [Fact]
    public void NormalizeSourceRect_WithPartiallyOutsideRect_ClampsToSource()
    {
        PixelSize sourceSize = new(100, 50);
        PixelRect sourceRect = new(-10, 20, 30, 40);

        PixelRect? normalizedRect = BitmapPixelCopy.NormalizeSourceRect(sourceSize, sourceRect);

        normalizedRect.Should().Be(new PixelRect(0, 20, 20, 30));
    }

    [Fact]
    public void EncodePixels_WithBgraPixels_PreservesPixelValues()
    {
        byte[] pixels =
        [
            11, 21, 32, 255, 12, 21, 33, 255,
            11, 22, 33, 255, 12, 22, 34, 255
        ];
        GCHandle pixelsHandle = GCHandle.Alloc(pixels, GCHandleType.Pinned);
        byte[] content;

        try
        {
            content = PngImageEncoder.EncodePixels(
                new PixelSize(2, 2),
                PixelFormat.Bgra8888,
                AlphaFormat.Premul,
                pixelsHandle.AddrOfPinnedObject(),
                8,
                CancellationToken.None);
        }
        finally
        {
            pixelsHandle.Free();
        }

        using SKBitmap decoded = SKBitmap.Decode(content)
            ?? throw new InvalidOperationException("Failed to decode the test PNG.");
        decoded.Width.Should().Be(2);
        decoded.Height.Should().Be(2);
        decoded.GetPixel(0, 0).Should().Be(new SKColor(32, 21, 11, 255));
        decoded.GetPixel(1, 1).Should().Be(new SKColor(34, 22, 12, 255));
    }

}
