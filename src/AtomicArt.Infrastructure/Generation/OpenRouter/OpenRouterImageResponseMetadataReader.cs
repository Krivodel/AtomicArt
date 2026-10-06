using System.Text.Json;

using AtomicArt.Application.Features.Generation.Models;
using AtomicArt.Contracts.Generation;

namespace AtomicArt.Infrastructure.Generation.OpenRouter;

internal sealed class OpenRouterImageResponseMetadataReader : IDisposable
{
    private readonly JsonStreamingResponseFilter _filter;
    private readonly int _maximumStructureDepth;

    public OpenRouterImageResponseMetadataReader(int maximumMetadataBytes, int maximumStructureDepth)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maximumStructureDepth, 1);
        _maximumStructureDepth = maximumStructureDepth;
        _filter = new JsonStreamingResponseFilter(
            new Dictionary<string, (string ReplacementValue, bool Sanitize)>(StringComparer.Ordinal)
            {
                ["b64_json"] = (string.Empty, false)
            },
            maximumMetadataBytes,
            CreateInvalidResponseException);
    }

    public void Append(ReadOnlySpan<byte> responseBytes)
    {
        _filter.Append(responseBytes);
    }

    public string ReadContentType()
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(
                _filter.Complete(),
                new JsonDocumentOptions { MaxDepth = _maximumStructureDepth });
            JsonElement root = document.RootElement;

            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("data", out JsonElement images)
                || images.ValueKind != JsonValueKind.Array
                || images.GetArrayLength() != 1
                || images[0].ValueKind != JsonValueKind.Object
                || !images[0].TryGetProperty("b64_json", out JsonElement imageData)
                || imageData.ValueKind != JsonValueKind.String)
            {
                throw CreateInvalidResponseException("The OpenRouter image response did not contain one image.");
            }

            if (!images[0].TryGetProperty("media_type", out JsonElement mediaType))
            {
                return GenerationImageContentTypes.Png;
            }

            string? contentType = mediaType.ValueKind == JsonValueKind.String ? mediaType.GetString() : null;

            if (!GenerationImageFileFormats.All.Any(format =>
                    string.Equals(format.ContentType, contentType, StringComparison.Ordinal)))
            {
                throw CreateInvalidResponseException("The OpenRouter image response declared an unsupported image type.");
            }

            return contentType ?? throw CreateInvalidResponseException("The OpenRouter image type is missing.");
        }
        catch (JsonException exception)
        {
            throw new OpenRouterImageException(
                ImageGenerationProviderFailureKind.InvalidResponse,
                "The OpenRouter image response contained malformed JSON.",
                false,
                exception);
        }
    }

    public void Dispose()
    {
        _filter.Dispose();
    }

    private static OpenRouterImageException CreateInvalidResponseException(string message)
    {
        return new OpenRouterImageException(ImageGenerationProviderFailureKind.InvalidResponse, message);
    }
}
