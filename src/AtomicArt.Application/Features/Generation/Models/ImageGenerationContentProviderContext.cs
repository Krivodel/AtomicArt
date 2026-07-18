using AtomicArt.Contracts.Generation;

namespace AtomicArt.Application.Features.Generation.Models;

public sealed record ImageGenerationContentProviderContext(
    ImageGenerationRequestDto Request,
    string Provider,
    string ProviderModelId,
    GenerationModelPricingMetadataDto Pricing,
    int ItemIndex,
    string? ProviderCredential);
