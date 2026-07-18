using AtomicArt.Domain.Common;

namespace AtomicArt.Domain.Generation;

public sealed class GenerationModelRules
{
    private readonly IReadOnlyList<IGenerationModelRules> _modelRules;

    public GenerationModelRules(IEnumerable<IGenerationModelRules> modelRules)
    {
        ArgumentNullException.ThrowIfNull(modelRules);

        _modelRules = CreateRulesSnapshot(modelRules);
    }

    public GenerationValidationResult Validate(GenerationValidationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        IGenerationModelRules modelRules = GetRules(request.Constraints);

        return modelRules.Validate(request);
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
        return UniqueHighestPrioritySelector.Select(
            _modelRules,
            rule => rule.CanValidate(constraints),
            rule => rule.Priority,
            () => new InvalidOperationException(
                $"No rules are registered for generation model '{constraints.ModelId}'."),
            priority => new InvalidOperationException(
                $"Multiple rules with priority {priority} are registered for generation model '{constraints.ModelId}'."));
    }
}
