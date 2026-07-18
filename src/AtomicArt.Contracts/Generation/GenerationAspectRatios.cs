namespace AtomicArt.Contracts.Generation;

public static class GenerationAspectRatios
{
    public const string Auto = "Авто";

    public static bool IsAuto(string? aspectRatio)
    {
        return string.Equals(aspectRatio, Auto, StringComparison.Ordinal);
    }
}
