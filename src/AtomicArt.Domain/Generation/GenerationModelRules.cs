namespace AtomicArt.Domain.Generation;

public sealed class GenerationModelRules
{
    private readonly IReadOnlyList<IGenerationModelRules> _modelRules;

    public GenerationModelRules(IEnumerable<IGenerationModelRules> modelRules)
    {
        ArgumentNullException.ThrowIfNull(modelRules);

        _modelRules = CreateRulesSnapshot(modelRules);
    }

    public GenerationValidationResult Validate(
        GenerationModelConstraints constraints,
        string? prompt,
        string aspectRatio,
        string resolution,
        double temperature,
        int generationCount,
        IReadOnlyList<GenerationAttachedImage> attachedImages,
        string? thinkingLevel = null)
    {
        ArgumentNullException.ThrowIfNull(constraints);
        ArgumentNullException.ThrowIfNull(aspectRatio);
        ArgumentNullException.ThrowIfNull(resolution);
        ArgumentNullException.ThrowIfNull(attachedImages);

        IGenerationModelRules modelRules = GetRules(constraints);

        return modelRules.Validate(
            constraints,
            prompt,
            aspectRatio,
            resolution,
            temperature,
            generationCount,
            attachedImages,
            thinkingLevel);
    }

    private static IReadOnlyList<IGenerationModelRules> CreateRulesSnapshot(
        IEnumerable<IGenerationModelRules> modelRules)
    {
        List<IGenerationModelRules> modelRulesList = modelRules.ToList();

        if (modelRulesList.Count == 0)
        {
            throw new InvalidOperationException(
                "No generation model rules are registered.");
        }

        if (modelRulesList.Any(rule => rule is null))
        {
            throw new InvalidOperationException(
                "Null generation model rules are registered.");
        }

        return modelRulesList;
    }

    private IGenerationModelRules GetRules(GenerationModelConstraints constraints)
    {
        List<IGenerationModelRules> matchingRules = _modelRules
            .Where(rule => rule.CanValidate(constraints))
            .OrderByDescending(rule => rule.Priority)
            .ToList();

        if (matchingRules.Count == 0)
        {
            throw new InvalidOperationException(
                $"No rules are registered for generation model '{constraints.ModelId}'.");
        }

        IGenerationModelRules selectedRules = matchingRules[0];

        if (matchingRules.Count > 1 && matchingRules[1].Priority == selectedRules.Priority)
        {
            throw new InvalidOperationException(
                $"Multiple rules with priority {selectedRules.Priority} are registered for generation model '{constraints.ModelId}'.");
        }

        return selectedRules;
    }
}
