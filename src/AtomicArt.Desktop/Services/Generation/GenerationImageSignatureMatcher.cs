using AtomicArt.Contracts.Generation;

namespace AtomicArt.Desktop.Services.Generation;

internal static class GenerationImageSignatureMatcher
{
    internal static bool Matches(
        IReadOnlyList<IReadOnlyList<GenerationImageFileSignaturePart>> signatureAlternatives,
        ReadOnlySpan<byte> content)
    {
        foreach (IReadOnlyList<GenerationImageFileSignaturePart> alternative in signatureAlternatives)
        {
            if (MatchesAlternative(content, alternative))
            {
                return true;
            }
        }

        return false;
    }

    private static bool MatchesAlternative(
        ReadOnlySpan<byte> content,
        IReadOnlyList<GenerationImageFileSignaturePart> signatureParts)
    {
        foreach (GenerationImageFileSignaturePart signaturePart in signatureParts)
        {
            if (content.Length < signaturePart.Offset + signaturePart.Bytes.Count)
            {
                return false;
            }

            if (!MatchesPart(content, signaturePart))
            {
                return false;
            }
        }

        return true;
    }

    private static bool MatchesPart(
        ReadOnlySpan<byte> content,
        GenerationImageFileSignaturePart signaturePart)
    {
        for (int index = 0; index < signaturePart.Bytes.Count; index++)
        {
            if (content[signaturePart.Offset + index] != signaturePart.Bytes[index])
            {
                return false;
            }
        }

        return true;
    }
}
