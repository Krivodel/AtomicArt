namespace AtomicArt.Desktop.Services.Dlss5;

public sealed class Dlss5SessionState
{
    public const double DefaultParameterAreaHeight = 180d;
    public const double MinimumParameterAreaHeight = 80d;
    public const double MaximumParameterAreaHeight = 4096d;

    public string? SourceFileName { get; init; }
    public Dlss5Style Style { get; init; } = Dlss5Style.Cinematic;
    public float GeneralIntensity { get; init; } = Dlss5RenderSettings.GeneralIntensityMaximum;
    public float LocalStructureIntensity { get; init; } = Dlss5RenderSettings.LocalStructureIntensityMaximum;
    public float SkinStructureStrength { get; init; } = Dlss5RenderSettings.SkinStructureStrengthMinimum;
    public float LocalToneStrength { get; init; } = Dlss5RenderSettings.Default.LocalToneStrength;
    public double ParameterAreaHeight { get; init; } = DefaultParameterAreaHeight;
}
