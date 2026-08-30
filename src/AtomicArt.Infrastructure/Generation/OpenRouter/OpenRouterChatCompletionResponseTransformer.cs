using System.Text;
using System.Text.Json;

using AtomicArt.Application.Features.Generation.Models;
using AtomicArt.Contracts.Generation;

namespace AtomicArt.Infrastructure.Generation.OpenRouter;

internal sealed class OpenRouterChatCompletionResponseTransformer : IDisposable
{
    private static readonly byte[] OutputPrefix = "{\"data\":[{\"b64_json\":\""u8.ToArray();

    private readonly MemoryStream _response = new();

    public Task AppendAsync(ReadOnlyMemory<byte> responseBytes, CancellationToken ct)
    {
        return _response.WriteAsync(responseBytes, ct).AsTask();
    }

    public async Task<string> CompleteAsync(Stream destination, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(destination);

        using JsonDocument document = JsonDocument.Parse(
            _response.GetBuffer().AsMemory(0, checked((int)_response.Length)));

        if (!TryFindLastImageDataUrl(document.RootElement, out string? imageDataUrl)
            || imageDataUrl is null
            || !TryParseImageDataUrl(
                imageDataUrl,
                out string? contentType,
                out string? base64Content)
            || contentType is null
            || base64Content is null)
        {
            throw new OpenRouterImageException(
                ImageGenerationProviderFailureKind.InvalidResponse,
                "The OpenRouter chat response did not contain an image data URL.");
        }

        await destination.WriteAsync(OutputPrefix, ct).ConfigureAwait(false);
        await destination.WriteAsync(Encoding.UTF8.GetBytes(base64Content), ct)
            .ConfigureAwait(false);

        string suffix = "\",\"media_type\":"
            + JsonSerializer.Serialize(contentType)
            + "}]}";
        await destination.WriteAsync(Encoding.UTF8.GetBytes(suffix), ct)
            .ConfigureAwait(false);

        return contentType;
    }

    public void Dispose()
    {
        _response.Dispose();
    }

    private static bool TryFindLastImageDataUrl(
        JsonElement element,
        out string? imageDataUrl)
    {
        imageDataUrl = null;
        bool found = false;

        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (JsonProperty property in element.EnumerateObject())
            {
                if (TryFindLastImageDataUrl(property.Value, out string? nestedImageDataUrl))
                {
                    imageDataUrl = nestedImageDataUrl;
                    found = true;
                }
            }

            return found;
        }

        if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement item in element.EnumerateArray())
            {
                if (TryFindLastImageDataUrl(item, out string? nestedImageDataUrl))
                {
                    imageDataUrl = nestedImageDataUrl;
                    found = true;
                }
            }

            return found;
        }

        if (element.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        string? value = element.GetString();

        if (value is null || !TryParseImageDataUrl(value, out _, out _))
        {
            return false;
        }

        imageDataUrl = value;
        return true;
    }

    private static bool TryParseImageDataUrl(
        string value,
        out string? contentType,
        out string? base64Content)
    {
        const string DataUrlPrefix = "data:";
        const string Base64Separator = ";base64,";
        contentType = null;
        base64Content = null;

        if (!value.StartsWith(DataUrlPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        int separatorIndex = value.IndexOf(
            Base64Separator,
            DataUrlPrefix.Length,
            StringComparison.OrdinalIgnoreCase);

        if (separatorIndex <= DataUrlPrefix.Length
            || separatorIndex + Base64Separator.Length >= value.Length)
        {
            return false;
        }

        string candidateContentType = value[
            DataUrlPrefix.Length..separatorIndex];

        if (!IsSupportedContentType(candidateContentType))
        {
            return false;
        }

        contentType = candidateContentType;
        base64Content = value[(separatorIndex + Base64Separator.Length)..];
        return true;
    }

    private static bool IsSupportedContentType(string contentType)
    {
        return contentType is GenerationImageContentTypes.Gif
            or GenerationImageContentTypes.Heic
            or GenerationImageContentTypes.Heif
            or GenerationImageContentTypes.Jpeg
            or GenerationImageContentTypes.Png
            or GenerationImageContentTypes.Webp;
    }
}
