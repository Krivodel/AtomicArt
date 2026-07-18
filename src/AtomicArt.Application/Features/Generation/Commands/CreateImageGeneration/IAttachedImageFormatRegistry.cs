using AtomicArt.Application.Features.Generation.Models;

namespace AtomicArt.Application.Features.Generation.Commands.CreateImageGeneration;

public interface IAttachedImageFormatRegistry
{
    IReadOnlyList<AttachedImageSignatureRule> GetSignatureRules();

    bool TryGetByContentType(string? contentType, out IAttachedImageFormat? format);
}
