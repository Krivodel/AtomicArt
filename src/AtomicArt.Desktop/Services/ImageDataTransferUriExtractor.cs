using System.Text;

using Avalonia.Input;

namespace AtomicArt.Desktop.Services;

internal static class ImageDataTransferUriExtractor
{
    private const string DataImagePrefix = "data:image/";

    private static readonly IReadOnlySet<string> UriFormatIdentifiers =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "DownloadURL",
            "public.url",
            "public.url-name",
            "text/uri-list",
            "text/x-moz-url",
            "UniformResourceLocator",
            "UniformResourceLocatorW"
        };

    private static readonly IReadOnlySet<string> HtmlFormatIdentifiers =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "HTML Format",
            "public.html",
            "text/html"
        };

    public static bool ContainsPotentialImageUri(IDataTransfer dataTransfer)
    {
        ArgumentNullException.ThrowIfNull(dataTransfer);

        return TryParseSupportedUri(dataTransfer.TryGetText(), out Uri? _)
            || dataTransfer.Formats.Any(format =>
                UriFormatIdentifiers.Contains(format.Identifier)
                || HtmlFormatIdentifiers.Contains(format.Identifier));
    }

    public static bool TryGetImageUri(
        IDataTransfer dataTransfer,
        out Uri? imageUri)
    {
        ArgumentNullException.ThrowIfNull(dataTransfer);

        if (TryParseSupportedUri(dataTransfer.TryGetText(), out imageUri))
        {
            return true;
        }

        foreach (IDataTransferItem item in dataTransfer.Items)
        {
            foreach (DataFormat format in item.Formats)
            {
                if (UriFormatIdentifiers.Contains(format.Identifier)
                    && TryGetText(item, format, out string uriText)
                    && TryParseUriFormat(format.Identifier, uriText, out imageUri))
                {
                    return true;
                }

                if (HtmlFormatIdentifiers.Contains(format.Identifier)
                    && TryGetText(item, format, out string html)
                    && HtmlImageSourceExtractor.TryExtract(html, out string? source)
                    && TryParseSupportedUri(source, out imageUri))
                {
                    return true;
                }
            }
        }

        imageUri = null;
        return false;
    }

    public static bool IsSupportedImageUri(Uri uri)
    {
        ArgumentNullException.ThrowIfNull(uri);

        return uri.IsAbsoluteUri
            && (string.Equals(
                    uri.Scheme,
                    Uri.UriSchemeHttp,
                    StringComparison.OrdinalIgnoreCase)
                || string.Equals(
                    uri.Scheme,
                    Uri.UriSchemeHttps,
                    StringComparison.OrdinalIgnoreCase)
                || (string.Equals(
                        uri.Scheme,
                        "data",
                        StringComparison.OrdinalIgnoreCase)
                    && uri.OriginalString.StartsWith(
                        DataImagePrefix,
                        StringComparison.OrdinalIgnoreCase)));
    }

    private static bool TryGetText(
        IDataTransferItem item,
        DataFormat format,
        out string text)
    {
        object? value = item.TryGetRaw(format);

        switch (value)
        {
            case string stringValue:
                text = stringValue.TrimEnd('\0');
                return !string.IsNullOrWhiteSpace(text);
            case byte[] bytes when bytes.Length > 0:
                text = DecodeText(format.Identifier, bytes);
                return !string.IsNullOrWhiteSpace(text);
            default:
                text = string.Empty;
                return false;
        }
    }

    private static string DecodeText(string formatIdentifier, byte[] bytes)
    {
        Encoding encoding = string.Equals(
                formatIdentifier,
                "UniformResourceLocatorW",
                StringComparison.OrdinalIgnoreCase)
            || LooksLikeUtf16(bytes)
            ? Encoding.Unicode
            : Encoding.UTF8;

        return encoding
            .GetString(bytes)
            .Trim('\uFEFF', '\0', ' ', '\t', '\r', '\n');
    }

    private static bool LooksLikeUtf16(byte[] bytes)
    {
        if (bytes.Length >= 2
            && ((bytes[0] == 0xFF && bytes[1] == 0xFE)
                || (bytes[0] == 0xFE && bytes[1] == 0xFF)))
        {
            return true;
        }

        int inspectedLength = Math.Min(bytes.Length, 64);
        int oddNullCount = 0;

        for (int index = 1; index < inspectedLength; index += 2)
        {
            if (bytes[index] == 0)
            {
                oddNullCount++;
            }
        }

        return inspectedLength >= 4
            && oddNullCount >= inspectedLength / 4;
    }

    private static bool TryParseUriFormat(
        string formatIdentifier,
        string value,
        out Uri? imageUri)
    {
        if (string.Equals(
                formatIdentifier,
                "DownloadURL",
                StringComparison.OrdinalIgnoreCase))
        {
            int firstSeparator = value.IndexOf(':');
            int secondSeparator = firstSeparator < 0
                ? -1
                : value.IndexOf(':', firstSeparator + 1);
            string downloadUri = secondSeparator < 0
                ? value
                : value[(secondSeparator + 1)..];

            return TryParseSupportedUri(downloadUri, out imageUri);
        }

        IEnumerable<string> candidates = value
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(candidate => candidate.Trim())
            .Where(candidate => !candidate.StartsWith('#'));

        foreach (string candidate in candidates)
        {
            if (TryParseSupportedUri(candidate, out imageUri))
            {
                return true;
            }
        }

        imageUri = null;
        return false;
    }

    private static bool TryParseSupportedUri(
        string? value,
        out Uri? imageUri)
    {
        string candidate = value?.Trim() ?? string.Empty;

        if (!Uri.TryCreate(candidate, UriKind.Absolute, out Uri? parsedUri)
            || !IsSupportedImageUri(parsedUri))
        {
            imageUri = null;
            return false;
        }

        imageUri = parsedUri;
        return true;
    }

}
