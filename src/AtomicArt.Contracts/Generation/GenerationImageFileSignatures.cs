namespace AtomicArt.Contracts.Generation;

public static class GenerationImageFileSignatures
{
    public const int HeifBrandOffset = 8;
    public const int WebpFormatOffset = 8;

    public static ReadOnlySpan<byte> Ftyp => [0x66, 0x74, 0x79, 0x70];
    public static ReadOnlySpan<byte> Gif87A => [0x47, 0x49, 0x46, 0x38, 0x37, 0x61];
    public static ReadOnlySpan<byte> Gif89A => [0x47, 0x49, 0x46, 0x38, 0x39, 0x61];
    public static ReadOnlySpan<byte> HeicBrand => [0x68, 0x65, 0x69, 0x63];
    public static ReadOnlySpan<byte> HeixBrand => [0x68, 0x65, 0x69, 0x78];
    public static ReadOnlySpan<byte> Jpeg => [0xFF, 0xD8, 0xFF];
    public static ReadOnlySpan<byte> Mif1Brand => [0x6D, 0x69, 0x66, 0x31];
    public static ReadOnlySpan<byte> Msf1Brand => [0x6D, 0x73, 0x66, 0x31];
    public static ReadOnlySpan<byte> Png => [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    public static ReadOnlySpan<byte> Riff => [0x52, 0x49, 0x46, 0x46];
    public static ReadOnlySpan<byte> Webp => [0x57, 0x45, 0x42, 0x50];
}
