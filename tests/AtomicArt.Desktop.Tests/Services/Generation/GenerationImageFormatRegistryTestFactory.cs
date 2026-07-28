using AtomicArt.Desktop.Services.Generation;

namespace AtomicArt.Desktop.Tests.Services.Generation;

internal static class GenerationImageFormatRegistryTestFactory
{
    public static IGenerationImageFormatRegistry Create()
    {
        IGenerationImageFormat[] formats =
        [
            new JpegGenerationImageFormat(),
            new PngGenerationImageFormat(),
            new WebpGenerationImageFormat()
        ];

        return new GenerationImageFormatRegistry(formats);
    }

    public static GenerationImageContentValidator CreateValidator()
    {
        return new GenerationImageContentValidator(
            Create(),
            TestApiConfiguration.MaxInputImageBytes);
    }

    public static GenerationImageContentValidator CreateValidator(int maxImageBytes)
    {
        return new GenerationImageContentValidator(Create(), maxImageBytes);
    }
}
