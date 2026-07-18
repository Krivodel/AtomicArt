using AtomicArt.Contracts.Generation;

namespace AtomicArt.Application.Features.Generation.Commands.CreateImageGeneration;

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
        return AttachedImageSignatureMatcher.Matches(_descriptor, content);
    }
}
