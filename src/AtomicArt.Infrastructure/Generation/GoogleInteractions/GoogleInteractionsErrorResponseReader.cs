using System.Text.Json;
using System.Text.RegularExpressions;

namespace AtomicArt.Infrastructure.Generation.GoogleInteractions;

internal static class GoogleInteractionsErrorResponseReader
{
    private static readonly Regex ProviderStatusRegex = new(
        @"^[A-Z0-9_]{1,64}$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex WhitespaceRegex = new(
        @"\s+",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static async Task<GoogleInteractionsErrorDiagnostics> ReadAsync(
        HttpContent content,
        int maxLoggedMessageCharacters,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentOutOfRangeException.ThrowIfLessThan(
            maxLoggedMessageCharacters,
            1);

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

        return Parse(body, maxLoggedMessageCharacters);
    }

    private static GoogleInteractionsErrorDiagnostics Parse(
        string body,
        int maxLoggedMessageCharacters)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(body);
            JsonElement root = document.RootElement;

            if (!GoogleInteractionsJsonElementReader.TryGetProperty(
                root,
                "error",
                out JsonElement errorElement))
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
                NormalizeAndLimit(
                    errorMessage,
                    maxLoggedMessageCharacters));
        }
        catch (JsonException)
        {
            return CreateMalformedDiagnostics(body.Length);
        }
    }

    private static int? GetErrorCode(JsonElement errorElement)
    {
        return GoogleInteractionsJsonElementReader.TryGetInt32Property(
            errorElement,
            "code",
            out int errorCode)
            ? errorCode
            : null;
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
        return GoogleInteractionsJsonElementReader.TryGetStringProperty(
            element,
            propertyName,
            out string? value)
            ? value
            : null;
    }

    private static string? NormalizeAndLimit(
        string? value,
        int maxLoggedMessageCharacters)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        string normalizedValue = NormalizeWhitespaceAndControlCharacters(value);

        return normalizedValue.Length <= maxLoggedMessageCharacters
            ? normalizedValue
            : normalizedValue[..maxLoggedMessageCharacters];
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
