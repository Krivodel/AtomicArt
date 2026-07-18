using AtomicArt.Application.Common.Models;
using AtomicArt.Contracts.Generation;
using AtomicArt.Domain.Generation;

namespace AtomicArt.Application.Features.Generation.Interfaces;

public interface IImageModelDefinition
{
    GenerationModelMetadataDto Metadata { get; }
    GenerationModelConstraints Constraints { get; }

    Result<ImageGenerationRequestDto> Validate(ImageGenerationRequestDto request);
}
