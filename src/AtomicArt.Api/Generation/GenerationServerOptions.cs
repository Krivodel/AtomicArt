namespace AtomicArt.Api.Generation;

public sealed class GenerationServerOptions
{
    public const string SectionName = "Generation";

    public int CopyBufferSize { get; init; }
    public long EmergencyMaxProviderResponseBytes { get; init; }
    public int MaximumBoundaryLength { get; init; }
    public int MaxConcurrentGenerations { get; init; }
    public int MaxMetadataBytes { get; init; }
    public long MaxRequestBytes { get; init; }

    public static bool IsValid(GenerationServerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return options.CopyBufferSize > 0
            && options.EmergencyMaxProviderResponseBytes > 0
            && options.MaximumBoundaryLength > 0
            && options.MaxConcurrentGenerations > 0
            && options.MaxMetadataBytes > 0
            && options.MaxRequestBytes > 0;
    }
}
