using AtomicArt.Contracts.Generation;

namespace AtomicArt.Desktop.Services.Generation;

internal sealed class PngGenerationImageFormat : GenerationImageFormat
{
    public PngGenerationImageFormat()
        : base(GetContractDescriptor(GenerationImageContentTypes.Png))
    {
    }
}
