using AtomicArt.Application.Features.Generation.Interfaces;
using AtomicArt.Application.Features.Generation.Models;

namespace AtomicArt.Infrastructure.Generation;

internal sealed class RoutingStreamingImageGenerationProvider
    : IStreamingImageGenerationProvider
{
    private readonly IReadOnlyDictionary<
        string,
        IProviderStreamingImageGenerationProvider> _providersById;

    public RoutingStreamingImageGenerationProvider(
        IEnumerable<IProviderStreamingImageGenerationProvider> providers)
    {
        ArgumentNullException.ThrowIfNull(providers);

        _providersById = providers.ToDictionary(
            provider => provider.Provider,
            provider => provider,
            StringComparer.Ordinal);
    }

    public Task<IProviderGenerationStream> CreateStreamAsync(
        StreamingGenerationProviderContext context,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (_providersById.TryGetValue(
                context.Provider,
                out IProviderStreamingImageGenerationProvider? provider))
        {
            return provider.CreateStreamAsync(context, ct);
        }

        throw new ImageGenerationProviderException(
            ImageGenerationProviderFailureKind.InvalidResponse,
            "The generation model provider is not supported.");
    }
}
