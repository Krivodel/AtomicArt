namespace AtomicArt.Application.Features.Generation.Models;

public enum ImageGenerationProviderFailureKind
{
    Authentication,
    Authorization,
    RateLimited,
    InvalidResponse,
    Timeout,
    Unavailable,
    RequestRejected,
    ResourceNotFound,
    InternalError,
    Unknown
}
