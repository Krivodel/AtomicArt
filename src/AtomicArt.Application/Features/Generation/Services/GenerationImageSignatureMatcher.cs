using AtomicArt.Contracts.Generation;

namespace AtomicArt.Application.Features.Generation.Services;

public static class GenerationImageSignatureMatcher
{
    public static bool Matches(
        GenerationImageFileFormatDescriptor descriptor,
        ReadOnlySpan<byte> content)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        foreach (IReadOnlyList<GenerationImageFileSignaturePart> alternative in descriptor.SignatureAlternatives)
        {
            if (MatchesSignatureAlternative(content, alternative))
            {
                return true;
            }
        }

        return false;
    }

    private static bool MatchesSignatureAlternative(
        ReadOnlySpan<byte> content,
        IReadOnlyList<GenerationImageFileSignaturePart> signatureParts)
    {
        foreach (GenerationImageFileSignaturePart signaturePart in signatureParts)
        {
            if (content.Length < signaturePart.Offset + signaturePart.Bytes.Count)
            {
                return false;
            }

            if (!MatchesSignaturePart(content, signaturePart))
            {
                return false;
            }
        }

        return true;
    }

    private static bool MatchesSignaturePart(
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
