using AtomicArt.Contracts.Generation;

namespace AtomicArt.Desktop.Services.Generation;

public sealed class GenerationClientOptions
{
    public const string SectionName = "Generation";
    public const int DefaultMaxConcurrentGenerations = 64;
    public const int DefaultMaxAutomaticRetries =
        GenerationAttemptLimits.MaximumAutomaticRetries;

    public int MaxConcurrentGenerations { get; set; } =
        DefaultMaxConcurrentGenerations;
    public int MaxAutomaticRetries { get; set; } =
        DefaultMaxAutomaticRetries;

    public static bool IsValid(GenerationClientOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return options.MaxConcurrentGenerations > 0
            && options.MaxAutomaticRetries >= 0
            && options.MaxAutomaticRetries <= DefaultMaxAutomaticRetries;
    }
}
