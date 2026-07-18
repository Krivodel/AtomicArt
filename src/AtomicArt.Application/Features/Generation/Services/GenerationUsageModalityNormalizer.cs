namespace AtomicArt.Application.Features.Generation.Services;

public static class GenerationUsageModalityNormalizer
{
    public static string Normalize(string modality)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modality);

        return modality.Trim().ToLowerInvariant();
    }
}
