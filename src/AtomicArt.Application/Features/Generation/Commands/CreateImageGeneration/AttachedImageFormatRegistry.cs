namespace AtomicArt.Application.Features.Generation.Commands.CreateImageGeneration;

public sealed class AttachedImageFormatRegistry : IAttachedImageFormatRegistry
{
    public int Count => _formats.Count;

    private readonly IReadOnlyList<IAttachedImageFormat> _formats;

    public AttachedImageFormatRegistry(IEnumerable<IAttachedImageFormat> formats)
    {
        ArgumentNullException.ThrowIfNull(formats);

        _formats = formats
            .OrderBy(format => format.ContentType, StringComparer.Ordinal)
            .ToList();
    }

    public bool TryGetByContentType(string? contentType, out IAttachedImageFormat? format)
    {
        format = null;

        if (string.IsNullOrWhiteSpace(contentType))
        {
            return false;
        }

        format = _formats.FirstOrDefault(candidateFormat =>
            candidateFormat.MatchesContentType(contentType.Trim()));

        return format is not null;
    }
}
