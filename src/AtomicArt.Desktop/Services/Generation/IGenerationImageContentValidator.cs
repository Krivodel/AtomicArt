using AtomicArt.Contracts.Generation;

namespace AtomicArt.Desktop.Services.Generation;

public interface IGenerationImageContentValidator
{
    bool TryValidate(
        GenerationImageContentDto? content,
        out GenerationImageContentValidationResult? result);
}
