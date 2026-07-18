using System.Text.Json;

using AtomicArt.Contracts.Generation;

namespace AtomicArt.Desktop.Services;

internal static class SafeApiProblemDetailsReader
{
    private const int MaxResponseBytes = 16 * 1024;
    private const int MaxErrorCodeLength = 32;
    private const string ErrorCodePrefix = "ERR-";

    public static async Task<string?> ReadErrorCodeAsync(
        HttpContent content,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(content);

        string? mediaType = content.Headers.ContentType?.MediaType;

        if (mediaType is null
            || (!mediaType.EndsWith("/json", StringComparison.OrdinalIgnoreCase)
                && !mediaType.EndsWith("+json", StringComparison.OrdinalIgnoreCase)))
        {
            return null;
        }

        await using Stream stream = await content
            .ReadAsStreamAsync(ct)
            .ConfigureAwait(false);
        byte[] buffer = new byte[MaxResponseBytes + 1];
        int totalBytesRead = 0;

        while (totalBytesRead < buffer.Length)
        {
            int bytesRead = await stream
                .ReadAsync(buffer.AsMemory(totalBytesRead, buffer.Length - totalBytesRead), ct)
                .ConfigureAwait(false);

            if (bytesRead == 0)
            {
                break;
            }

            totalBytesRead += bytesRead;
        }

        if (totalBytesRead == 0
            || totalBytesRead > MaxResponseBytes)
        {
            return null;
        }

        using JsonDocument document = JsonDocument.Parse(buffer.AsMemory(0, totalBytesRead));

        if (document.RootElement.ValueKind != JsonValueKind.Object
            || !document.RootElement.TryGetProperty(
                GenerationApiRoutes.ProblemDetailsErrorCodeExtensionName,
                out JsonElement errorCodeElement)
            || errorCodeElement.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        string? errorCode = errorCodeElement.GetString();

        return IsSafeErrorCode(errorCode)
            ? errorCode
            : null;
    }

    private static bool IsSafeErrorCode(string? errorCode)
    {
        if (string.IsNullOrWhiteSpace(errorCode)
            || errorCode.Length > MaxErrorCodeLength
            || !errorCode.StartsWith(ErrorCodePrefix, StringComparison.Ordinal))
        {
            return false;
        }

        return errorCode.All(character =>
            character is >= 'A' and <= 'Z'
                or >= '0' and <= '9'
                or '-');
    }
}
