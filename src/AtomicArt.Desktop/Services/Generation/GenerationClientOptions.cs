using AtomicArt.Contracts.Generation;

namespace AtomicArt.Desktop.Services.Generation;

public sealed class GenerationClientOptions
{
    public const string SectionName = "Generation";

    public int AttachedImagePreparationConcurrency { get; init; }
    public int Base64DecoderInputBufferSize { get; init; }
    public int Base64DecoderOutputBufferSize { get; init; }
    public int EncodingProbeActivationPixelMultiplier { get; init; }
    public int EncodingProbeMaximumDimension { get; init; }
    public int ExternalImageTimeoutSeconds { get; init; }
    public int FastLosslessWebpCompressionEffort { get; init; }
    public int FastPngCompressionLevel { get; init; }
    public int LossyQualitySearchSteps { get; init; }
    public int MaxAutomaticRetries { get; init; }
    public int MaxConcurrentGenerations { get; init; }
    public long MaxDecodedProviderResponseImageBytes { get; init; }
    public int MaxInputImageBytes { get; init; }
    public double MaximumLosslessCandidateRatio { get; init; }
    public int MaximumLossyQuality { get; init; }
    public int MaximumLosslessWebpCompressionEffort { get; init; }
    public int MaximumPngCompressionLevel { get; init; }
    public int MaximumResizeAttempts { get; init; }
    public int MaxResponseMetadataBytes { get; init; }
    public int MinimumLossyQuality { get; init; }
    public int ProviderResponseTimeoutSeconds { get; init; }
    public double ResizeSafetyFactor { get; init; }
    public int ResponseMetadataBufferSize { get; init; }

    public static bool IsValid(GenerationClientOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return options.AttachedImagePreparationConcurrency >= 0
            && options.Base64DecoderInputBufferSize > 0
            && options.Base64DecoderOutputBufferSize >= 3
            && options.EncodingProbeActivationPixelMultiplier > 0
            && options.EncodingProbeMaximumDimension > 0
            && options.ExternalImageTimeoutSeconds > 0
            && options.FastLosslessWebpCompressionEffort is >= 0 and <= 100
            && options.FastPngCompressionLevel is >= 0 and <= 9
            && options.LossyQualitySearchSteps > 0
            && options.MaxAutomaticRetries >= 0
            && options.MaxAutomaticRetries
                <= GenerationAttemptLimits.MaximumAutomaticRetries
            && options.MaxConcurrentGenerations > 0
            && options.MaxDecodedProviderResponseImageBytes > 0
            && options.MaxInputImageBytes > 0
            && options.MaximumLosslessCandidateRatio >= 1d
            && options.MaximumLossyQuality is > 0 and <= 100
            && options.MaximumLosslessWebpCompressionEffort is >= 0 and <= 100
            && options.MaximumPngCompressionLevel is >= 0 and <= 9
            && options.MaximumResizeAttempts > 0
            && options.MaxResponseMetadataBytes > 0
            && options.MinimumLossyQuality > 0
            && options.MinimumLossyQuality < options.MaximumLossyQuality
            && options.ProviderResponseTimeoutSeconds > 0
            && options.ResizeSafetyFactor is > 0d and <= 1d
            && options.ResponseMetadataBufferSize > 0;
    }
}
