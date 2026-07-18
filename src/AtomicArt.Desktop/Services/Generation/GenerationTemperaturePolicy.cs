using AtomicArt.Contracts.Generation;

namespace AtomicArt.Desktop.Services.Generation;

internal static class GenerationTemperaturePolicy
{
    private const double ComparisonTolerance = 0.000000001d;

    internal static bool IsSupported(
        double value,
        GenerationModelTemperatureMetadataDto temperature)
    {
        ArgumentNullException.ThrowIfNull(temperature);

        if (!double.IsFinite(value)
            || value < temperature.Minimum - ComparisonTolerance
            || value > temperature.Maximum + ComparisonTolerance)
        {
            return false;
        }

        double stepCount = (value - temperature.Minimum) / temperature.Step;

        return Math.Abs(stepCount - Math.Round(stepCount)) <= ComparisonTolerance;
    }
}
