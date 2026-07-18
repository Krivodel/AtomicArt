using AtomicArt.Contracts.Generation;

namespace AtomicArt.Desktop.Services.Generation;

internal sealed class WebpGenerationImageFormat : GenerationImageFormat
{
    public WebpGenerationImageFormat()
        : base(GetContractDescriptor(GenerationImageContentTypes.Webp))
    {
    }
}
