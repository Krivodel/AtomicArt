using AtomicArt.Application.Common.Models;
using AtomicArt.Contracts.Generation;

namespace AtomicArt.Application.Features.Generation.Interfaces;

public interface IImageGenerationRequestNormalizer
{
    Result<ImageGenerationRequestDto> Normalize(
        ImageGenerationRequestDto request,
        IImageModelDefinition modelDefinition);
}
