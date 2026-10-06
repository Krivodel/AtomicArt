using System.Text.Json;

using AtomicArt.Application.Features.Generation.Models;

namespace AtomicArt.Infrastructure.Generation.GoogleInteractions;

internal sealed class GoogleStreamingResponseAnalyzer : IDisposable
{
    private readonly GoogleInteractionsResponseParser _responseParser;
    private readonly GoogleInteractionsFailureClassifier _failureClassifier;
    private readonly JsonStreamingResponseFilter _filter;
    private readonly int _maximumStructureDepth;
    private readonly int _maximumDiagnosticTextCharacters;

    public GoogleStreamingResponseAnalyzer(
        GoogleInteractionsResponseParser responseParser,
        GoogleInteractionsFailureClassifier failureClassifier,
        int maximumFilteredResponseBytes,
        int maximumStructureDepth,
        int maximumDiagnosticTextCharacters)
    {
        _responseParser = responseParser
            ?? throw new ArgumentNullException(nameof(responseParser));
        _failureClassifier = failureClassifier
            ?? throw new ArgumentNullException(nameof(failureClassifier));
        ArgumentOutOfRangeException.ThrowIfLessThan(maximumStructureDepth, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(maximumDiagnosticTextCharacters, 1);
        _maximumStructureDepth = maximumStructureDepth;
        _maximumDiagnosticTextCharacters = maximumDiagnosticTextCharacters;
        _filter = new JsonStreamingResponseFilter(
            new Dictionary<string, (string ReplacementValue, bool Sanitize)>(StringComparer.Ordinal)
            {
                [GoogleInteractionsContentContract.DataPropertyName] = ("AA==", false),
                [GoogleInteractionsContentContract.SignaturePropertyName] = (string.Empty, true)
            },
            maximumFilteredResponseBytes,
            CreateInvalidResponseException);
    }

    public void Append(ReadOnlySpan<byte> content)
    {
        _filter.Append(content);
    }

    public int AppendAndSanitize(Span<byte> content)
    {
        return _filter.AppendAndSanitize(content);
    }

    public ProviderGenerationSummary Complete()
    {
        ReadOnlyMemory<byte> filteredJson = _filter.Complete();

        try
        {
            using JsonDocument document = JsonDocument.Parse(
                filteredJson,
                new JsonDocumentOptions
                {
                    MaxDepth = _maximumStructureDepth
                });
            JsonElement root = document.RootElement;

            ThrowIfDiagnosticTextExceedsLimit(root);
            ThrowIfTemporaryInternalError(root);

            return _responseParser.ParseFilteredMetadata(root);
        }
        catch (JsonException exception)
        {
            throw new GoogleInteractionsException(
                ImageGenerationProviderFailureKind.InvalidResponse,
                "The generation provider returned malformed JSON.",
                false,
                exception);
        }
    }

    public void Dispose()
    {
        _filter.Dispose();
    }

    private static GoogleInteractionsException CreateInvalidResponseException(string message)
    {
        return new GoogleInteractionsException(ImageGenerationProviderFailureKind.InvalidResponse, message);
    }

    private void ThrowIfTemporaryInternalError(JsonElement root)
    {
        if (_failureClassifier.IsTemporaryInternalError(root))
        {
            throw new GoogleInteractionsException(
                ImageGenerationProviderFailureKind.InternalError,
                "The generation provider returned a temporary internal error.",
                true);
        }
    }

    private void ThrowIfDiagnosticTextExceedsLimit(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                bool isTextContent =
                    GoogleInteractionsContentContract.IsTextContent(element);

                foreach (JsonProperty property in element.EnumerateObject())
                {
                    if (isTextContent
                        && property.NameEquals(
                            GoogleInteractionsContentContract
                                .TextPropertyName))
                    {
                        continue;
                    }

                    ThrowIfDiagnosticTextExceedsLimit(property.Value);
                }

                break;
            case JsonValueKind.Array:
                foreach (JsonElement item in element.EnumerateArray())
                {
                    ThrowIfDiagnosticTextExceedsLimit(item);
                }

                break;
            case JsonValueKind.String:
                if ((element.GetString()?.Length ?? 0)
                    > _maximumDiagnosticTextCharacters)
                {
                    throw new GoogleInteractionsException(
                        ImageGenerationProviderFailureKind.InvalidResponse,
                        "The generation provider response contains diagnostic text that exceeds its limit.");
                }

                break;
        }
    }

}
