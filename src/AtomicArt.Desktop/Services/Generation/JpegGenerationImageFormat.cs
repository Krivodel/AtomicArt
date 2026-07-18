using AtomicArt.Contracts.Generation;

namespace AtomicArt.Desktop.Services.Generation;

internal sealed class JpegGenerationImageFormat : GenerationImageFormat
{
    public JpegGenerationImageFormat()
        : base(GetContractDescriptor(GenerationImageContentTypes.Jpeg))
    {
    }
}
