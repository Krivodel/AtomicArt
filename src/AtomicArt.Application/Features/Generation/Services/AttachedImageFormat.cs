using AtomicArt.Application.Features.Generation.Services;
using AtomicArt.Contracts.Generation;
using AtomicArt.Application.Features.Generation.Interfaces;

namespace AtomicArt.Application.Features.Generation.Services;

public sealed class AttachedImageFormat : IAttachedImageFormat
{
    private readonly GenerationImageFileFormatDescriptor _descriptor;

    public AttachedImageFormat(GenerationImageFileFormatDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentException.ThrowIfNullOrWhiteSpace(descriptor.ContentType);

        _descriptor = descriptor;
    }

    public string ContentType => _descriptor.ContentType;

    public bool MatchesContentType(string contentType)
    {
        return string.Equals(ContentType, contentType, StringComparison.OrdinalIgnoreCase);
    }

    public bool MatchesSignature(ReadOnlySpan<byte> content)
    {
        return GenerationImageSignatureMatcher.Matches(_descriptor, content);
    }
}
