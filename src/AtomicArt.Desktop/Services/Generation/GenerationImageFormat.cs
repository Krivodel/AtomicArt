using AtomicArt.Contracts.Generation;

namespace AtomicArt.Desktop.Services.Generation;

internal abstract class GenerationImageFormat : IGenerationImageFormat
{
    public string ContentType { get; }
    public string Extension { get; }

    private readonly IReadOnlyList<string> _extensions;
    private readonly IReadOnlyList<IReadOnlyList<GenerationImageFileSignaturePart>> _signatureAlternatives;

    protected GenerationImageFormat(GenerationImageFileFormatDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        ContentType = descriptor.ContentType;
        Extension = descriptor.Extensions.First();
        _extensions = descriptor.Extensions;
        _signatureAlternatives = descriptor.SignatureAlternatives;
    }

    public bool MatchesContentType(string contentType)
    {
        return string.Equals(ContentType, contentType, StringComparison.OrdinalIgnoreCase);
    }

    public bool MatchesFileName(string fileName)
    {
        string extension = Path.GetExtension(fileName);

        return _extensions.Any(candidateExtension =>
            string.Equals(candidateExtension, extension, StringComparison.OrdinalIgnoreCase));
    }

    public bool MatchesSignature(ReadOnlySpan<byte> bytes)
    {
        foreach (IReadOnlyList<GenerationImageFileSignaturePart> signatureAlternative in _signatureAlternatives)
        {
            if (MatchesSignatureAlternative(bytes, signatureAlternative))
            {
                return true;
            }
        }

        return false;
    }

    protected static GenerationImageFileFormatDescriptor GetContractDescriptor(string contentType)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);

        return GenerationImageFileFormats.All.Single(format =>
            string.Equals(format.ContentType, contentType, StringComparison.Ordinal));
    }

    private static bool MatchesSignatureAlternative(
        ReadOnlySpan<byte> bytes,
        IReadOnlyList<GenerationImageFileSignaturePart> signatureParts)
    {
        foreach (GenerationImageFileSignaturePart signaturePart in signatureParts)
        {
            if (bytes.Length < signaturePart.Offset + signaturePart.Bytes.Count)
            {
                return false;
            }

            if (!MatchesSignaturePart(bytes, signaturePart))
            {
                return false;
            }
        }

        return true;
    }

    private static bool MatchesSignaturePart(
        ReadOnlySpan<byte> bytes,
        GenerationImageFileSignaturePart signaturePart)
    {
        for (int i = 0; i < signaturePart.Bytes.Count; i++)
        {
            if (bytes[signaturePart.Offset + i] != signaturePart.Bytes[i])
            {
                return false;
            }
        }

        return true;
    }
}
