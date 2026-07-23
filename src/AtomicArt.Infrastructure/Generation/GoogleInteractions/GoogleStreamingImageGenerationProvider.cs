using Microsoft.Extensions.Options;

using AtomicArt.Application.Features.Generation.Interfaces;
using AtomicArt.Application.Features.Generation.Models;
using AtomicArt.Contracts.Generation;

namespace AtomicArt.Infrastructure.Generation.GoogleInteractions;

internal sealed class GoogleStreamingImageGenerationProvider
    : IProviderStreamingImageGenerationProvider
{
    public string Provider => GenerationProviderIds.Google;

    private readonly IGoogleInteractionsClient _client;
    private readonly GoogleInteractionsResponseParser _responseParser;
    private readonly GoogleInteractionsFailureClassifier _failureClassifier;
    private readonly GoogleInteractionsOptions _options;

    public GoogleStreamingImageGenerationProvider(
        IGoogleInteractionsClient client,
        GoogleInteractionsResponseParser responseParser,
        GoogleInteractionsFailureClassifier failureClassifier,
        IOptions<GoogleInteractionsOptions> options)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _responseParser = responseParser
            ?? throw new ArgumentNullException(nameof(responseParser));
        _failureClassifier = failureClassifier
            ?? throw new ArgumentNullException(nameof(failureClassifier));
        ArgumentNullException.ThrowIfNull(options);
        _options = options.Value;
    }

    public async Task<IProviderGenerationStream> CreateStreamAsync(
        StreamingGenerationProviderContext context,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (!string.Equals(
                context.Provider,
                GenerationProviderIds.Google,
                StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(context.ProviderCredential))
        {
            throw new GoogleInteractionsException(
                ImageGenerationProviderFailureKind.Authentication,
                "The temporary provider credential was not supplied.");
        }

        GoogleInteractionsStreamingContent content = new(context);

        long maximumRequestBytes = context.TransportLimits is null
            ? _options.MaxRequestBytes
            : Math.Min(
                _options.MaxRequestBytes,
                context.TransportLimits.MaxRequestBytes);

        if (content.Headers.ContentLength > maximumRequestBytes)
        {
            content.Dispose();

            throw new GoogleInteractionsException(
                ImageGenerationProviderFailureKind.RequestRejected,
                "The provider request exceeds the configured provider limit.");
        }

        try
        {
            GoogleInteractionsStreamingResponse response = await _client
                .CreateInteractionStreamAsync(
                    content,
                    context.ProviderCredential,
                    ct)
                .ConfigureAwait(false);

            int maximumAnalyzedMetadataBytes =
                context.TransportLimits is null
                    ? _options.MaxAnalyzedMetadataBytes
                    : Math.Min(
                        _options.MaxAnalyzedMetadataBytes,
                        context.TransportLimits.MaxStatisticsBytes);
            int maximumStructureDepth = context.TransportLimits is null
                ? _options.MaxResponseStructureDepth
                : Math.Min(
                    _options.MaxResponseStructureDepth,
                    context.TransportLimits.MaxStructureDepth);
            int maximumDiagnosticTextCharacters =
                context.TransportLimits is null
                    ? _options.MaxDiagnosticTextCharacters
                    : Math.Min(
                        _options.MaxDiagnosticTextCharacters,
                        context.TransportLimits.MaxDiagnosticTextCharacters);

            return new GoogleProviderGenerationStream(
                response,
                _responseParser,
                _failureClassifier,
                _options.MaxResponseBytes,
                maximumAnalyzedMetadataBytes,
                maximumStructureDepth,
                maximumDiagnosticTextCharacters);
        }
        catch
        {
            content.Dispose();
            throw;
        }
    }
}
