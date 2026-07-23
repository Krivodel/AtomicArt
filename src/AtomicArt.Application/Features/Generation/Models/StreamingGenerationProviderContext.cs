using AtomicArt.Contracts.Generation;

namespace AtomicArt.Application.Features.Generation.Models;

public sealed record StreamingGenerationProviderContext(
    StreamingImageGenerationRequest Request,
    string Provider,
    string ProviderModelId,
    GenerationModelPricingMetadataDto Pricing,
    string? ProviderCredential,
    GenerationModelTransportLimitsDto? TransportLimits = null);
