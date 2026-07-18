using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using AtomicArt.Application.Features.Generation.Interfaces;
using AtomicArt.Application.Features.Generation.Models;

namespace AtomicArt.Infrastructure.Generation;

internal sealed class RoutingImageGenerationContentProvider : IImageGenerationContentProvider
{
    private readonly IReadOnlyDictionary<string, IProviderImageGenerationContentProvider> _providersById;
    private readonly ILogger<RoutingImageGenerationContentProvider> _logger;

    public RoutingImageGenerationContentProvider(IEnumerable<IProviderImageGenerationContentProvider> providers)
        : this(providers, NullLogger<RoutingImageGenerationContentProvider>.Instance)
    {
    }

    public RoutingImageGenerationContentProvider(
        IEnumerable<IProviderImageGenerationContentProvider> providers,
        ILogger<RoutingImageGenerationContentProvider> logger)
    {
        ArgumentNullException.ThrowIfNull(providers);
        ArgumentNullException.ThrowIfNull(logger);

        _providersById = providers.ToDictionary(
            provider => provider.Provider,
            provider => provider,
            StringComparer.Ordinal);
        _logger = logger;
    }

    public Task<ImageGenerationContentResult> GetContentAsync(
        ImageGenerationContentProviderContext context,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (_providersById.TryGetValue(context.Provider, out IProviderImageGenerationContentProvider? provider))
        {
            _logger.LogDebug(
                "Generation request was routed to registered provider {Provider}.",
                provider.Provider);

            return provider.GetContentAsync(context, ct);
        }

        _logger.LogWarning(
            "Generation request could not be routed because no matching provider is registered.");

        throw new ImageGenerationProviderException(
            ImageGenerationProviderFailureKind.InvalidResponse,
            "The generation model provider is not supported.");
    }
}
