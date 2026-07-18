namespace AtomicArt.Application.Features.Generation.Commands.CreateImageGeneration;

public interface IAttachedImageFormatRegistry
{
    int Count { get; }

    bool TryGetByContentType(string? contentType, out IAttachedImageFormat? format);
}
