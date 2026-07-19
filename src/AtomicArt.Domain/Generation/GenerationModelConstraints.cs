using System.Numerics;

using AtomicArt.Domain.Exceptions;

namespace AtomicArt.Domain.Generation;

public sealed record GenerationModelConstraints
{
    public string ModelId { get; }
    public int MaxPromptLength { get; }
    public IReadOnlyList<string> AspectRatios { get; }
    public IReadOnlyList<string> Resolutions { get; }
    public IReadOnlyList<int> GenerationCounts { get; }
    public GenerationModelTemperatureConstraints Temperature { get; }
    public GenerationModelThinkingConstraints? Thinking { get; }
    public int MaxAttachedImages { get; }
    public long MaxAttachedImageBytes { get; }
    public long MaxTotalAttachedImageBytes { get; }
    public IReadOnlyList<string> SupportedContentTypes { get; }

    public GenerationModelConstraints(
        string? modelId,
        int maxPromptLength,
        IReadOnlyList<string> aspectRatios,
        IReadOnlyList<string> resolutions,
        IReadOnlyList<int> generationCounts,
        GenerationModelTemperatureConstraints temperature,
        int maxAttachedImages,
        long maxAttachedImageBytes,
        long maxTotalAttachedImageBytes,
        IReadOnlyList<string> supportedContentTypes,
        GenerationModelThinkingConstraints? thinking = null)
    {
        if (string.IsNullOrWhiteSpace(modelId))
        {
            throw new DomainException(
                GenerationErrorCodes.InvalidModelId,
                "The generation model identifier must not be empty.");
        }

        ModelId = modelId.Trim();
        MaxPromptLength = RequirePositive(maxPromptLength, nameof(maxPromptLength));
        AspectRatios = CreateRequiredSnapshot(aspectRatios, nameof(aspectRatios));
        Resolutions = CreateRequiredSnapshot(resolutions, nameof(resolutions));
        GenerationCounts = CreateRequiredPositiveSnapshot(generationCounts, nameof(generationCounts));
        Temperature = temperature ?? throw new DomainException(
            GenerationErrorCodes.MissingConstraintValues,
            "Generation model constraint 'temperature' is required.");
        MaxAttachedImages = RequirePositive(maxAttachedImages, nameof(maxAttachedImages));
        MaxAttachedImageBytes = RequirePositive(maxAttachedImageBytes, nameof(maxAttachedImageBytes));
        MaxTotalAttachedImageBytes = RequirePositive(maxTotalAttachedImageBytes, nameof(maxTotalAttachedImageBytes));
        SupportedContentTypes = CreateRequiredSnapshot(supportedContentTypes, nameof(supportedContentTypes));
        Thinking = thinking;

        if (MaxTotalAttachedImageBytes < MaxAttachedImageBytes)
        {
            throw new DomainException(
                GenerationErrorCodes.InvalidAttachmentTotalLimit,
                "Total attachment byte limit cannot be lower than the single attachment byte limit.");
        }
    }

    public bool Equals(GenerationModelConstraints? other)
    {
        return other is not null
            && string.Equals(ModelId, other.ModelId, StringComparison.Ordinal)
            && MaxPromptLength == other.MaxPromptLength
            && AspectRatios.SequenceEqual(other.AspectRatios)
            && Resolutions.SequenceEqual(other.Resolutions)
            && GenerationCounts.SequenceEqual(other.GenerationCounts)
            && Temperature == other.Temperature
            && Thinking == other.Thinking
            && MaxAttachedImages == other.MaxAttachedImages
            && MaxAttachedImageBytes == other.MaxAttachedImageBytes
            && MaxTotalAttachedImageBytes == other.MaxTotalAttachedImageBytes
            && SupportedContentTypes.SequenceEqual(other.SupportedContentTypes);
    }

    public override int GetHashCode()
    {
        HashCode hashCode = new();

        hashCode.Add(ModelId, StringComparer.Ordinal);
        hashCode.Add(MaxPromptLength);
        AddValues(hashCode, AspectRatios, StringComparer.Ordinal);
        AddValues(hashCode, Resolutions, StringComparer.Ordinal);
        AddValues(hashCode, GenerationCounts);
        hashCode.Add(Temperature);
        hashCode.Add(Thinking);
        hashCode.Add(MaxAttachedImages);
        hashCode.Add(MaxAttachedImageBytes);
        hashCode.Add(MaxTotalAttachedImageBytes);
        AddValues(hashCode, SupportedContentTypes, StringComparer.Ordinal);

        return hashCode.ToHashCode();
    }

    private static TNumber RequirePositive<TNumber>(TNumber value, string parameterName)
        where TNumber : INumber<TNumber>
    {
        if (value <= TNumber.Zero)
        {
            throw CreateInvalidPositiveLimitException(parameterName);
        }

        return value;
    }

    private static DomainException CreateInvalidPositiveLimitException(string parameterName)
    {
        return new DomainException(
            GenerationErrorCodes.InvalidConstraintLimit,
            $"Generation model constraint '{parameterName}' must be positive.");
    }

    private static IReadOnlyList<string> CreateRequiredSnapshot(
        IReadOnlyList<string> values,
        string parameterName)
    {
        EnsureHasValues(values, parameterName, "must contain at least one value.");

        string[] snapshot = new string[values.Count];

        for (int index = 0; index < values.Count; index++)
        {
            string? value = values[index];

            if (string.IsNullOrWhiteSpace(value))
            {
                throw new DomainException(
                    GenerationErrorCodes.MissingConstraintValues,
                    $"Generation model constraint '{parameterName}' contains an empty value.");
            }

            snapshot[index] = value.Trim();
        }

        return Array.AsReadOnly(snapshot);
    }

    private static IReadOnlyList<int> CreateRequiredPositiveSnapshot(
        IReadOnlyList<int> values,
        string parameterName)
    {
        EnsureHasValues(values, parameterName, "must contain at least one positive value.");

        int[] snapshot = new int[values.Count];

        for (int index = 0; index < values.Count; index++)
        {
            int value = values[index];

            if (value <= 0)
            {
                throw new DomainException(
                    GenerationErrorCodes.InvalidConstraintLimit,
                    $"Generation model constraint '{parameterName}' contains a non-positive value.");
            }

            snapshot[index] = value;
        }

        return Array.AsReadOnly(snapshot);
    }

    private static void EnsureHasValues<T>(
        IReadOnlyList<T> values,
        string parameterName,
        string requirement)
    {
        if (values is null || values.Count == 0)
        {
            throw new DomainException(
                GenerationErrorCodes.MissingConstraintValues,
                $"Generation model constraint '{parameterName}' {requirement}");
        }
    }

    private static void AddValues<T>(
        HashCode hashCode,
        IReadOnlyList<T> values,
        IEqualityComparer<T>? comparer = null)
    {
        foreach (T value in values)
        {
            hashCode.Add(value, comparer);
        }
    }
}
