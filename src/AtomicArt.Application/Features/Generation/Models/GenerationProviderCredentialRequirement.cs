namespace AtomicArt.Application.Features.Generation.Models;

public sealed record GenerationProviderCredentialRequirement(
    bool RequiredAtApiBoundary,
    bool RequiredForApplicationValidation);
