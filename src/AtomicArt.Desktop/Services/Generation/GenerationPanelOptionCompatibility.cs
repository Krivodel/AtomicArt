using AtomicArt.Contracts.Generation;

namespace AtomicArt.Desktop.Services.Generation;

internal static class GenerationPanelOptionCompatibility
{
    private const int TemperatureNormalizationDigits = 12;
    private const double TemperatureComparisonTolerance = 0.000000001d;

    public static (string Value, bool WasReset) ResolveString(
        string? value,
        IReadOnlyList<string> supportedValues,
        string defaultValue)
    {
        ArgumentNullException.ThrowIfNull(supportedValues);
        ArgumentNullException.ThrowIfNull(defaultValue);

        if (!string.IsNullOrWhiteSpace(value)
            && supportedValues.Contains(value, StringComparer.Ordinal))
        {
            return (value, false);
        }

        return (defaultValue, !string.IsNullOrWhiteSpace(value));
    }

    public static (int Value, bool WasReset) ResolveGenerationCount(
        int value,
        ImageModelOption selectedModel)
    {
        ArgumentNullException.ThrowIfNull(selectedModel);

        if (selectedModel.GenerationCounts.Contains(value))
        {
            return (value, false);
        }

        return (GenerationPanelOptionDefaults.GetDefaultGenerationCount(selectedModel), value > 0);
    }

    public static (double Value, bool WasReset) ResolveTemperature(
        double? value,
        GenerationModelTemperatureMetadataDto temperature)
    {
        ArgumentNullException.ThrowIfNull(temperature);

        if (value is { } selectedTemperature
            && IsSupportedTemperature(selectedTemperature, temperature))
        {
            return (NormalizeTemperature(selectedTemperature, temperature), false);
        }

        return (temperature.Default, value.HasValue);
    }

    public static (string? Value, bool WasReset) ResolveThinkingLevel(
        string? value,
        GenerationModelThinkingMetadataDto? thinking)
    {
        if (thinking is null)
        {
            return (null, !string.IsNullOrWhiteSpace(value));
        }

        IReadOnlyList<string> supportedValues = thinking.Levels
            .Select(level => level.Value)
            .ToList();
        (string resolvedValue, bool wasReset) = ResolveString(
            value,
            supportedValues,
            thinking.Default);

        return (resolvedValue, wasReset);
    }

    public static (string? Value, bool WasReset) ResolveRememberedThinkingLevel(
        string? value,
        ImageModelOption selectedModel,
        IReadOnlyList<ImageModelOption> panelModels)
    {
        ArgumentNullException.ThrowIfNull(selectedModel);
        ArgumentNullException.ThrowIfNull(panelModels);

        if (selectedModel.Thinking is not null)
        {
            return ResolveThinkingLevel(value, selectedModel.Thinking);
        }

        if (string.IsNullOrWhiteSpace(value))
        {
            return (null, false);
        }

        bool isSupportedByPanel = panelModels
            .Where(model => model.Thinking is not null)
            .SelectMany(model => model.Thinking?.Levels
                ?? [])
            .Any(level => string.Equals(level.Value, value, StringComparison.Ordinal));

        return isSupportedByPanel
            ? (value, false)
            : (null, true);
    }

    private static bool IsSupportedTemperature(
        double value,
        GenerationModelTemperatureMetadataDto temperature)
    {
        if (!double.IsFinite(value)
            || value < temperature.Minimum - TemperatureComparisonTolerance
            || value > temperature.Maximum + TemperatureComparisonTolerance)
        {
            return false;
        }

        double stepCount = (value - temperature.Minimum) / temperature.Step;

        return Math.Abs(stepCount - Math.Round(stepCount)) <= TemperatureComparisonTolerance;
    }

    private static double NormalizeTemperature(
        double value,
        GenerationModelTemperatureMetadataDto temperature)
    {
        double stepCount = Math.Round((value - temperature.Minimum) / temperature.Step);
        double normalizedValue = temperature.Minimum + (stepCount * temperature.Step);

        return Math.Round(
            Math.Clamp(normalizedValue, temperature.Minimum, temperature.Maximum),
            TemperatureNormalizationDigits,
            MidpointRounding.AwayFromZero);
    }
}
