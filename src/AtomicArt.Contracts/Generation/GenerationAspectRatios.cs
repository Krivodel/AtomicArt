namespace AtomicArt.Contracts.Generation;

public static class GenerationAspectRatios
{
    public const string Auto = "auto";
    public const string GptAutoFallback = "16:9";

    public static bool IsAuto(string? aspectRatio)
    {
        return string.Equals(aspectRatio, Auto, StringComparison.Ordinal);
    }
}
