using AtomicArt.Contracts.Generation;
using AtomicArt.Infrastructure.Generation;

namespace AtomicArt.Tests.Common.Generation;

public static class GenerationImageTestData
{
    public const int TestMaxImageBytes = 16;
    public const int TestOversizedBase64Length = 28;

    public static int PngSignatureLength => PngSignatureBytesValue.Length;
    public static byte[] MinimalPngBytes => CreatePngContent(PngSignatureLength + 1);
    public static byte[] PngSignatureBytes => (byte[])PngSignatureBytesValue.Clone();
    public static byte[] ValidPngBytes => (byte[])ValidPngBytesValue.Clone();

    private static readonly byte[] PngSignatureBytesValue =
        GenerationImageFileSignatures.Png.ToArray();
    private static readonly byte[] ValidPngBytesValue = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+/p9sAAAAASUVORK5CYII=");

    public static AttachedImageDto CreateAttachedImage(string fileName)
    {
        return new AttachedImageDto(
            fileName,
            GenerationImageContentTypes.Png,
            ValidPngBytes);
    }

    public static byte[] CreatePngContent(int length)
    {
        byte[] content = new byte[length];
        PngSignatureBytesValue.CopyTo(content, 0);

        return content;
    }
}
