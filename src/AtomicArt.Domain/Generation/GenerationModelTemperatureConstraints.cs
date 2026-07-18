using AtomicArt.Domain.Exceptions;

namespace AtomicArt.Domain.Generation;

public sealed record GenerationModelTemperatureConstraints
{
    private const string InvalidTemperatureBoundsErrorCode = "ERR-GEN-104";
    private const string InvalidTemperatureDefaultErrorCode = "ERR-GEN-105";
    private const string InvalidTemperatureStepErrorCode = "ERR-GEN-106";
    private const double ComparisonTolerance = 0.000000001d;

    public double Minimum { get; }
    public double Maximum { get; }
    public double Default { get; }
    public double Step { get; }

    public GenerationModelTemperatureConstraints(
        double minimum,
        double maximum,
        double defaultValue,
        double step)
    {
        if (!double.IsFinite(minimum)
            || !double.IsFinite(maximum)
            || minimum < 0d
            || maximum <= minimum)
        {
            throw new DomainException(
                InvalidTemperatureBoundsErrorCode,
                "Model temperature bounds must define a finite non-negative range.");
        }

        if (!double.IsFinite(step)
            || step <= 0d
            || step > maximum - minimum)
        {
            throw new DomainException(
                InvalidTemperatureStepErrorCode,
                "The model temperature step must be positive and must not exceed the range.");
        }

        Minimum = minimum;
        Maximum = maximum;
        Step = step;

        if (!IsSupported(maximum))
        {
            throw new DomainException(
                InvalidTemperatureStepErrorCode,
                "The model temperature maximum must align with the configured step.");
        }

        if (!IsSupported(defaultValue))
        {
            throw new DomainException(
                InvalidTemperatureDefaultErrorCode,
                "The default model temperature must align with the allowed range and step.");
        }

        Default = defaultValue;
    }

    public bool IsSupported(double value)
    {
        if (!double.IsFinite(value)
            || value < Minimum - ComparisonTolerance
            || value > Maximum + ComparisonTolerance)
        {
            return false;
        }

        double stepCount = (value - Minimum) / Step;

        return Math.Abs(stepCount - Math.Round(stepCount)) <= ComparisonTolerance;
    }
}
