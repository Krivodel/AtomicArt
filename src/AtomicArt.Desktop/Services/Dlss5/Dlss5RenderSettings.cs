namespace AtomicArt.Desktop.Services.Dlss5;

public sealed record Dlss5RenderSettings(
    Dlss5Style Style,
    float GeneralIntensity,
    float LocalStructureIntensity,
    float SkinStructureStrength,
    float LocalToneStrength)
{
    public const float GeneralIntensityMinimum = 0f;
    public const float GeneralIntensityMaximum = 2f;
    public const float LocalStructureIntensityMinimum = 0f;
    public const float LocalStructureIntensityMaximum = 2f;
    public const float SkinStructureStrengthMinimum = -1f;
    public const float SkinStructureStrengthMaximum = 2f;
    public const float LocalToneStrengthMinimum = 0f;
    public const float LocalToneStrengthMaximum = 2f;

    public static Dlss5RenderSettings Default { get; } = new(
        Dlss5Style.Cinematic,
        Dlss5RenderSettings.GeneralIntensityMaximum,
        Dlss5RenderSettings.LocalStructureIntensityMaximum,
        Dlss5RenderSettings.SkinStructureStrengthMinimum,
        DefaultLocalToneStrength);

    private const float DefaultLocalToneStrength = 1f;
    private const int NativePrecisionDigits = 4;

    public void Validate()
    {
        if (!Enum.IsDefined(Style))
        {
            throw new ArgumentOutOfRangeException(nameof(Style), Style, "DLSS 5 style is not supported.");
        }

        Dlss5RenderSettings.ValidateRange(
            GeneralIntensity,
            GeneralIntensityMinimum,
            GeneralIntensityMaximum,
            nameof(GeneralIntensity));
        Dlss5RenderSettings.ValidateRange(
            LocalStructureIntensity,
            LocalStructureIntensityMinimum,
            LocalStructureIntensityMaximum,
            nameof(LocalStructureIntensity));
        Dlss5RenderSettings.ValidateRange(
            SkinStructureStrength,
            SkinStructureStrengthMinimum,
            SkinStructureStrengthMaximum,
            nameof(SkinStructureStrength));
        Dlss5RenderSettings.ValidateRange(
            LocalToneStrength,
            LocalToneStrengthMinimum,
            LocalToneStrengthMaximum,
            nameof(LocalToneStrength));
    }

    // Keep worker/profile precision aligned; slider bindings can otherwise
    // turn the same visible value into a needless cold-start.
    internal Dlss5RenderSettings NormalizeForNative()
    {
        Validate();
        return this with
        {
            GeneralIntensity = Dlss5RenderSettings.Normalize(GeneralIntensity),
            LocalStructureIntensity = Dlss5RenderSettings.Normalize(LocalStructureIntensity),
            SkinStructureStrength = Dlss5RenderSettings.Normalize(SkinStructureStrength),
            LocalToneStrength = Dlss5RenderSettings.Normalize(LocalToneStrength)
        };
    }

    private static void ValidateRange(float value, float minimum, float maximum, string name)
    {
        if ((!float.IsFinite(value)) || (value < minimum) || (value > maximum))
        {
            throw new ArgumentOutOfRangeException(name);
        }
    }

    private static float Normalize(float value)
    {
        return MathF.Round(value, NativePrecisionDigits, MidpointRounding.ToEven);
    }
}
