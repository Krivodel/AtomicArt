using System.Text.Json;
using System.Text.RegularExpressions;

namespace AtomicArt.Infrastructure.Generation.GoogleInteractions;

internal static class GoogleInteractionsErrorResponseReader
{
    private const int MaxLoggedMessageCharacters = 512;

    private static readonly Regex ProviderStatusRegex = new(
        @"^[A-Z0-9_]{1,64}$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex WhitespaceRegex = new(
        @"\s+",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static async Task<GoogleInteractionsErrorDiagnostics> ReadAsync(
        HttpContent content,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(content);

        string body = await content
            .ReadAsStringAsync(ct)
            .ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(body))
        {
            return new GoogleInteractionsErrorDiagnostics(
                GoogleInteractionsErrorBodyKind.Empty,
                body.Length,
                null,
                null,
                null);
        }

        return Parse(body);
    }

    private static GoogleInteractionsErrorDiagnostics Parse(string body)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(body);
            JsonElement root = document.RootElement;

            if (!TryGetProperty(root, "error", out JsonElement errorElement))
            {
                return CreateMalformedDiagnostics(body.Length);
            }

            int? errorCode = GetErrorCode(errorElement);
            string? errorStatus = GetStringProperty(errorElement, "status");
            string? errorMessage = GetErrorMessage(errorElement);

            return new GoogleInteractionsErrorDiagnostics(
                GoogleInteractionsErrorBodyKind.Parsed,
                body.Length,
                errorCode,
                SanitizeProviderStatus(errorStatus),
                NormalizeAndLimit(errorMessage));
        }
        catch (JsonException)
        {
            return CreateMalformedDiagnostics(body.Length);
        }
    }

    private static int? GetErrorCode(JsonElement errorElement)
    {
        if (errorElement.ValueKind != JsonValueKind.Object
            || !TryGetProperty(errorElement, "code", out JsonElement codeElement)
            || codeElement.ValueKind != JsonValueKind.Number
            || !codeElement.TryGetInt32(out int errorCode))
        {
            return null;
        }

        return errorCode;
    }

    private static string? GetErrorMessage(JsonElement errorElement)
    {
        if (errorElement.ValueKind == JsonValueKind.String)
        {
            return errorElement.GetString();
        }

        return GetStringProperty(errorElement, "message");
    }

    private static string? GetStringProperty(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object
            || !TryGetProperty(element, propertyName, out JsonElement propertyElement)
            || propertyElement.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        return propertyElement.GetString();
    }

    private static string? NormalizeAndLimit(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        string normalizedValue = NormalizeWhitespaceAndControlCharacters(value);

        return normalizedValue.Length <= MaxLoggedMessageCharacters
            ? normalizedValue
            : normalizedValue[..MaxLoggedMessageCharacters];
    }

    private static string NormalizeWhitespaceAndControlCharacters(string value)
    {
        char[] characters = value
            .Select(character => char.IsControl(character) ? ' ' : character)
            .ToArray();
        string normalizedValue = WhitespaceRegex
            .Replace(new string(characters), " ")
            .Trim();

        return normalizedValue;
    }

    private static string? SanitizeProviderStatus(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return null;
        }

        string normalizedStatus = status.Trim();

        return ProviderStatusRegex.IsMatch(normalizedStatus)
            ? normalizedStatus
            : null;
    }

    private static bool TryGetProperty(
        JsonElement element,
        string propertyName,
        out JsonElement value)
    {
        if (element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(propertyName, out value))
        {
            return true;
        }

        value = default;

        return false;
    }

    private static GoogleInteractionsErrorDiagnostics CreateMalformedDiagnostics(
        int characterCount)
    {
        return new GoogleInteractionsErrorDiagnostics(
            GoogleInteractionsErrorBodyKind.Malformed,
            characterCount,
            null,
            null,
            null);
    }
}
