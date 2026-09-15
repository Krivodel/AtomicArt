using AtomicArt.Desktop.Services.State;

namespace AtomicArt.Desktop.Services.Dlss5;

public sealed class Dlss5SessionStateSection : StateSection<Dlss5SessionState>
{
    public const string KeyValue = "dlss5-session";

    private const string SectionFileName = "dlss5-session.json";
    private const int CurrentSchemaVersion = 1;

    public Dlss5SessionStateSection()
        : base(KeyValue, SectionFileName, CurrentSchemaVersion)
    {
    }

    protected override Dlss5SessionState NormalizePayload(Dlss5SessionState? state)
    {
        Dlss5SessionState source = state ?? new Dlss5SessionState();
        string? fileName = string.IsNullOrWhiteSpace(source.SourceFileName)
            ? null
            : Path.GetFileName(source.SourceFileName);

        return new Dlss5SessionState
        {
            SourceFileName = fileName,
            Style = Enum.IsDefined(source.Style) ? source.Style : Dlss5Style.Cinematic,
            GeneralIntensity = Math.Clamp(
                source.GeneralIntensity,
                Dlss5RenderSettings.GeneralIntensityMinimum,
                Dlss5RenderSettings.GeneralIntensityMaximum),
            LocalStructureIntensity = Math.Clamp(
                source.LocalStructureIntensity,
                Dlss5RenderSettings.LocalStructureIntensityMinimum,
                Dlss5RenderSettings.LocalStructureIntensityMaximum),
            SkinStructureStrength = Math.Clamp(
                source.SkinStructureStrength,
                Dlss5RenderSettings.SkinStructureStrengthMinimum,
                Dlss5RenderSettings.SkinStructureStrengthMaximum),
            LocalToneStrength = Math.Clamp(
                source.LocalToneStrength,
                Dlss5RenderSettings.LocalToneStrengthMinimum,
                Dlss5RenderSettings.LocalToneStrengthMaximum),
            ParameterAreaHeight = double.IsFinite(source.ParameterAreaHeight)
                ? Math.Clamp(
                    source.ParameterAreaHeight,
                    Dlss5SessionState.MinimumParameterAreaHeight,
                    Dlss5SessionState.MaximumParameterAreaHeight)
                : Dlss5SessionState.DefaultParameterAreaHeight
        };
    }
}
