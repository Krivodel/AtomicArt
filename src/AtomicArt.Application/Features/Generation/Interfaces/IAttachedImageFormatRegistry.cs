namespace AtomicArt.Application.Features.Generation.Interfaces;

public interface IAttachedImageFormatRegistry
{
    int Count { get; }

    bool TryGetByContentType(string? contentType, out IAttachedImageFormat? format);
}
