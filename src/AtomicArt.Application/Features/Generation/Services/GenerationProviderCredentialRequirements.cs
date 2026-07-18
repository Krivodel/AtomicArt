using AtomicArt.Application.Features.Generation.Models;
using AtomicArt.Contracts.Generation;

namespace AtomicArt.Application.Features.Generation.Services;

public static class GenerationProviderCredentialRequirements
{
    private static readonly GenerationProviderCredentialRequirement GoogleRequirement =
        new(true, true);
    private static readonly GenerationProviderCredentialRequirement TestRequirement =
        new(false, false);
    private static readonly GenerationProviderCredentialRequirement OtherRequirement =
        new(true, false);

    public static GenerationProviderCredentialRequirement Resolve(string? provider)
    {
        if (string.Equals(provider, GenerationProviderIds.Google, StringComparison.Ordinal))
        {
            return GoogleRequirement;
        }

        if (string.Equals(provider, GenerationProviderIds.Test, StringComparison.Ordinal))
        {
            return TestRequirement;
        }

        return OtherRequirement;
    }
}
