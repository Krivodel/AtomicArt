using AtomicArt.Application.Common.Models;
using AtomicArt.Application.Features.Generation.Interfaces;
using AtomicArt.Contracts.Generation;

namespace AtomicArt.Application.Features.Generation.Services;

public sealed class ImageGenerationRequestNormalizer : IImageGenerationRequestNormalizer
{
    public Result<ImageGenerationRequestDto> Normalize(
        ImageGenerationRequestDto request,
        IImageModelDefinition modelDefinition)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(modelDefinition);

        return modelDefinition.Validate(request);
    }
}
