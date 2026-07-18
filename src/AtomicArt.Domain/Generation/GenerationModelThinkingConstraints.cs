using AtomicArt.Domain.Exceptions;

namespace AtomicArt.Domain.Generation;

public sealed record GenerationModelThinkingConstraints
{
    private const string InvalidMetadataErrorCode = "ERR-GEN-111";

    public IReadOnlyList<string> Levels { get; }
    public string Default { get; }

    public GenerationModelThinkingConstraints(
        IReadOnlyList<string>? levels,
        string defaultValue)
    {
        Levels = CreateLevelsSnapshot(levels);
        Default = RequireSupportedDefault(defaultValue, Levels);
    }

    public bool IsSupported(string? value)
    {
        return !string.IsNullOrWhiteSpace(value)
            && Levels.Contains(value.Trim(), StringComparer.Ordinal);
    }

    public bool Equals(GenerationModelThinkingConstraints? other)
    {
        return other is not null
            && Levels.SequenceEqual(other.Levels, StringComparer.Ordinal)
            && string.Equals(Default, other.Default, StringComparison.Ordinal);
    }

    public override int GetHashCode()
    {
        HashCode hashCode = new();

        foreach (string level in Levels)
        {
            hashCode.Add(level, StringComparer.Ordinal);
        }

        hashCode.Add(Default, StringComparer.Ordinal);

        return hashCode.ToHashCode();
    }

    private static IReadOnlyList<string> CreateLevelsSnapshot(IReadOnlyList<string>? levels)
    {
        if (levels is null || levels.Count == 0)
        {
            throw new DomainException(
                InvalidMetadataErrorCode,
                "Generation model thinking levels must contain at least one value.");
        }

        List<string> snapshot = [];
        HashSet<string> uniqueValues = new(StringComparer.Ordinal);

        foreach (string? level in levels)
        {
            if (string.IsNullOrWhiteSpace(level))
            {
                throw new DomainException(
                    InvalidMetadataErrorCode,
                    "Generation model thinking levels contain an empty value.");
            }

            string normalizedLevel = level.Trim();

            if (!uniqueValues.Add(normalizedLevel))
            {
                throw new DomainException(
                    InvalidMetadataErrorCode,
                    $"Generation model thinking level '{normalizedLevel}' is duplicated.");
            }

            snapshot.Add(normalizedLevel);
        }

        return snapshot.AsReadOnly();
    }

    private static string RequireSupportedDefault(
        string defaultValue,
        IReadOnlyList<string> levels)
    {
        if (string.IsNullOrWhiteSpace(defaultValue))
        {
            throw new DomainException(
                InvalidMetadataErrorCode,
                "Generation model default thinking level is required.");
        }

        string normalizedDefault = defaultValue.Trim();

        if (!levels.Contains(normalizedDefault, StringComparer.Ordinal))
        {
            throw new DomainException(
                InvalidMetadataErrorCode,
                $"Generation model default thinking level '{normalizedDefault}' is not supported.");
        }

        return normalizedDefault;
    }
}
