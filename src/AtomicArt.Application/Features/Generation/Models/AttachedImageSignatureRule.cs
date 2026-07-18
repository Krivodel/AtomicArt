namespace AtomicArt.Application.Features.Generation.Models;

public sealed record AttachedImageSignatureRule(
    string ContentType,
    Func<byte[], bool> Matches);
