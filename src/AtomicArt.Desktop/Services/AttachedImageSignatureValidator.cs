using AtomicArt.Contracts.Generation;
using AtomicArt.Desktop.Services.Generation;

namespace AtomicArt.Desktop.Services;

public sealed class AttachedImageSignatureValidator : IAttachedImageSignatureValidator
{
    private static readonly IReadOnlyDictionary<string, GenerationImageFileFormatDescriptor> FormatsByExtension =
        CreateFormatsByExtension();
    private static readonly IReadOnlyDictionary<string, GenerationImageFileFormatDescriptor> FormatsByContentType =
        GenerationImageFileFormats
            .All
            .ToDictionary(format => format.ContentType, StringComparer.OrdinalIgnoreCase);

    public bool TryGetContentType(string fileName, ReadOnlySpan<byte> content, out string contentType)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        string extension = Path.GetExtension(fileName);

        if (!FormatsByExtension.TryGetValue(extension, out GenerationImageFileFormatDescriptor? format)
            || !MatchesSignature(format, content))
        {
            contentType = string.Empty;
            return false;
        }

        contentType = format.ContentType;
        return true;
    }

    public bool MatchesSignature(string contentType, ReadOnlySpan<byte> content)
    {
        if (string.IsNullOrWhiteSpace(contentType))
        {
            return false;
        }

        return FormatsByContentType.TryGetValue(contentType.Trim(), out GenerationImageFileFormatDescriptor? format)
            && MatchesSignature(format, content);
    }

    private static IReadOnlyDictionary<string, GenerationImageFileFormatDescriptor> CreateFormatsByExtension()
    {
        Dictionary<string, GenerationImageFileFormatDescriptor> formats =
            new Dictionary<string, GenerationImageFileFormatDescriptor>(StringComparer.OrdinalIgnoreCase);

        foreach (GenerationImageFileFormatDescriptor format in GenerationImageFileFormats.All)
        {
            foreach (string extension in format.Extensions)
            {
                formats.Add(extension, format);
            }
        }

        return formats;
    }

    private static bool MatchesSignature(
        GenerationImageFileFormatDescriptor format,
        ReadOnlySpan<byte> content)
    {
        return GenerationImageSignatureMatcher.Matches(
            format.SignatureAlternatives,
            content);
    }
}
