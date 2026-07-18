namespace AtomicArt.Application.Features.Generation.Models;

internal sealed record AttachedImageValidationOptions(
    IReadOnlyList<AttachedImageSignatureRule> SignatureRules);
