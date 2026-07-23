namespace AtomicArt.Api.Generation;

public sealed class GenerationServerOptions
{
    public const string SectionName = "Generation";
    public const int DefaultMaxConcurrentGenerations = 64;

    public int MaxConcurrentGenerations { get; set; } =
        DefaultMaxConcurrentGenerations;

    public static bool IsValid(GenerationServerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return options.MaxConcurrentGenerations > 0;
    }
}
