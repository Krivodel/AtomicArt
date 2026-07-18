namespace AtomicArt.Domain.Generation;

public interface IGenerationModelRules
{
    int Priority { get; }

    bool CanValidate(GenerationModelConstraints constraints);

    GenerationValidationResult Validate(
        GenerationModelConstraints constraints,
        string? prompt,
        string aspectRatio,
        string resolution,
        double temperature,
        int generationCount,
        IReadOnlyList<GenerationAttachedImage> attachedImages,
        string? thinkingLevel = null);
}
