namespace AtomicArt.Domain.Generation;

public interface IGenerationModelRules
{
    int Priority { get; }

    bool CanValidate(GenerationModelConstraints constraints);

    GenerationValidationResult Validate(GenerationValidationRequest request);
}
