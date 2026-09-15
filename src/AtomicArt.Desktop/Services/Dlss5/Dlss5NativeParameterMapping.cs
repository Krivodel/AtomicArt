namespace AtomicArt.Desktop.Services.Dlss5;

// Keep this map aligned with v7's <14I4f> worker header; values are already in the UI scale.
internal readonly record struct Dlss5NativeParameterMapping(
    int Style,
    float GeneralIntensity,
    float LocalToneStrength,
    float LocalStructureStrength,
    float SkinStructureStrength,
    int AutomaticMask)
{
    public const uint FixedDlssModelPreset = 0;
    public const uint FixedNrPreset = 0;
    public const uint FixedProfile = 0;
    public const uint FixedUiCorrection = 0;
    public const uint DlaaPerformanceQuality = 5;
    public const uint NoWarmupFrames = 0;
    public const int AutomaticMaskOff = 0;

    public static Dlss5NativeParameterMapping Create(Dlss5RenderSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        settings = settings.NormalizeForNative();

        return new Dlss5NativeParameterMapping(
            (int)settings.Style,
            settings.GeneralIntensity,
            settings.LocalToneStrength,
            settings.LocalStructureIntensity,
            settings.SkinStructureStrength,
            AutomaticMaskOff);
    }
}
