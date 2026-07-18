namespace AtomicArt.Desktop.Tests.Services.Generation;

internal static class GenerationImageTestData
{
    public const int TestMaxImageBytes = 16;
    public const int TestOversizedBase64Length = 28;

    public static byte[] ValidPngBytes => (byte[])ValidPngBytesValue.Clone();

    private static readonly byte[] ValidPngBytesValue = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+/p9sAAAAASUVORK5CYII=");
}
