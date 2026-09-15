using System.Buffers.Binary;

using FluentAssertions;
using SkiaSharp;
using Xunit;

using AtomicArt.Desktop.Services.Windows;

namespace AtomicArt.Desktop.Tests.Services.Windows;

public sealed class WindowsClipboardDibCodecTests
{
    private const int BitmapV5HeaderSize = 124;

    [Fact]
    public void ConvertToPng_WithDibV5_PreservesDimensionsAndPixels()
    {
        byte[] dib = CreateDibV5();

        byte[] png = WindowsClipboardDibCodec.ConvertToPng(dib, 1024);
        using SKBitmap bitmap = SKBitmap.Decode(png)
            ?? throw new InvalidOperationException("Converted PNG should decode.");

        bitmap.Width.Should().Be(2);
        bitmap.Height.Should().Be(1);
        bitmap.GetPixel(0, 0).Should().Be(new SKColor(255, 0, 0, 255));
        bitmap.GetPixel(1, 0).Should().Be(new SKColor(0, 0, 255, 255));
    }

    [Fact]
    public void ConvertToPng_WithIncompleteHeader_ThrowsInvalidDataException()
    {
        Action action = () => WindowsClipboardDibCodec.ConvertToPng([1, 2, 3], 1024);

        action.Should().Throw<InvalidDataException>();
    }

    [Theory]
    [InlineData(1, 4)]
    [InlineData(1024, 4096)]
    [InlineData(int.MaxValue, int.MaxValue)]
    public void GetMaximumDibBytes_ReturnsBoundedExpandedLimit(
        int maxOutputBytes,
        int expectedMaximumBytes)
    {
        int actualMaximumBytes = WindowsClipboardDibCodec.GetMaximumDibBytes(
            maxOutputBytes);

        actualMaximumBytes.Should().Be(expectedMaximumBytes);
    }

    private static byte[] CreateDibV5()
    {
        const int pixelBytes = 8;
        byte[] dib = new byte[BitmapV5HeaderSize + pixelBytes];
        Span<byte> header = dib.AsSpan(0, BitmapV5HeaderSize);
        BinaryPrimitives.WriteUInt32LittleEndian(header, BitmapV5HeaderSize);
        BinaryPrimitives.WriteInt32LittleEndian(header[4..], 2);
        BinaryPrimitives.WriteInt32LittleEndian(header[8..], -1);
        BinaryPrimitives.WriteUInt16LittleEndian(header[12..], 1);
        BinaryPrimitives.WriteUInt16LittleEndian(header[14..], 32);
        BinaryPrimitives.WriteUInt32LittleEndian(header[16..], 3);
        BinaryPrimitives.WriteUInt32LittleEndian(header[20..], pixelBytes);
        BinaryPrimitives.WriteUInt32LittleEndian(header[40..], 0x00FF0000);
        BinaryPrimitives.WriteUInt32LittleEndian(header[44..], 0x0000FF00);
        BinaryPrimitives.WriteUInt32LittleEndian(header[48..], 0x000000FF);
        BinaryPrimitives.WriteUInt32LittleEndian(header[52..], 0xFF000000);
        BinaryPrimitives.WriteUInt32LittleEndian(header[56..], 0x73524742);
        Span<byte> pixels = dib.AsSpan(BitmapV5HeaderSize);
        byte[] redAndBluePixels =
        [
            0x00, 0x00, 0xFF, 0xFF,
            0xFF, 0x00, 0x00, 0xFF
        ];
        redAndBluePixels.CopyTo(pixels);

        return dib;
    }
}
