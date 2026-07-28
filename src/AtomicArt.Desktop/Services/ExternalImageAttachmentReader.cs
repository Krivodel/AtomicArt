using System.Net.Http.Headers;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using AtomicArt.Contracts.Generation;

namespace AtomicArt.Desktop.Services;

public sealed class ExternalImageAttachmentReader
{
    private const string ExternalImageFileName = "dropped-image";
    private const string UnknownImageContentType = "application/octet-stream";
    private const string ExternalImageTooLargeMessage =
        "External image exceeds the safe input size limit.";
    private const string InvalidDataImageMessage =
        "Dropped data URI does not contain a valid image.";
    private readonly HttpClient _httpClient;
    private readonly IAttachedImageSignatureValidator _signatureValidator;
    private readonly ILogger<ExternalImageAttachmentReader> _logger;
    private readonly int _maximumFileNameCharacters;

    public ExternalImageAttachmentReader(
        HttpClient httpClient,
        IAttachedImageSignatureValidator signatureValidator,
        ILogger<ExternalImageAttachmentReader> logger,
        IOptions<DataTransferOptions> options)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(signatureValidator);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(options);

        _httpClient = httpClient;
        _signatureValidator = signatureValidator;
        _logger = logger;
        _maximumFileNameCharacters =
            options.Value.MaximumTransferredFileNameCharacters;
    }

    public ImageAttachmentInput CreateInput(
        Uri imageUri,
        int maxInputBytes)
    {
        ArgumentNullException.ThrowIfNull(imageUri);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxInputBytes);

        if (!ImageDataTransferUriExtractor.IsSupportedImageUri(imageUri))
        {
            throw new ArgumentException(
                "Only HTTP, HTTPS, and image data URIs are supported.",
                nameof(imageUri));
        }

        string fileName = BuildFileName(
            imageUri,
            _maximumFileNameCharacters);

        return new ImageAttachmentInput(
            fileName,
            ct => ReadAsync(imageUri, fileName, maxInputBytes, ct));
    }

    private async Task<AttachedImageDto?> ReadAsync(
        Uri imageUri,
        string fileName,
        int maxInputBytes,
        CancellationToken ct)
    {
        if (string.Equals(
                imageUri.Scheme,
                "data",
                StringComparison.OrdinalIgnoreCase))
        {
            return ReadDataUri(imageUri, fileName, maxInputBytes, ct);
        }

        using HttpRequestMessage request = new(HttpMethod.Get, imageUri);
        using HttpResponseMessage response = await _httpClient
            .SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                ct)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        long? contentLength = response.Content.Headers.ContentLength;

        if (contentLength > maxInputBytes)
        {
            _logger.LogWarning(
                "External image response with {SizeBytes} bytes exceeded the input limit of {MaxInputBytes} bytes.",
                contentLength,
                maxInputBytes);
            throw new InvalidDataException(ExternalImageTooLargeMessage);
        }

        await using Stream input = await response.Content
            .ReadAsStreamAsync(ct)
            .ConfigureAwait(false);
        byte[] content = await LimitedContentReader
            .ReadAsync(
                input,
                maxInputBytes,
                ExternalImageTooLargeMessage,
                ct)
            .ConfigureAwait(false);
        string contentType = ResolveContentType(
            response.Content.Headers.ContentType,
            content);
        _logger.LogInformation(
            "External image read with {SizeBytes} bytes and content type {ContentType}.",
            content.LongLength,
            contentType);

        return new AttachedImageDto(fileName, contentType, content);
    }

    private AttachedImageDto ReadDataUri(
        Uri imageUri,
        string fileName,
        int maxInputBytes,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        string value = imageUri.OriginalString;
        int separatorIndex = value.IndexOf(',');

        if (separatorIndex <= 0)
        {
            throw new InvalidDataException(InvalidDataImageMessage);
        }

        string metadata = value[..separatorIndex];

        if (!metadata.EndsWith(";base64", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(InvalidDataImageMessage);
        }

        string declaredContentType = metadata[5..^7]
            .Split(';', 2)[0];
        string base64Data = value[(separatorIndex + 1)..];
        int maximumBase64Length = checked(((maxInputBytes + 2) / 3) * 4);

        if (base64Data.Length > maximumBase64Length)
        {
            throw new InvalidDataException(ExternalImageTooLargeMessage);
        }

        if (base64Data.Contains('%', StringComparison.Ordinal))
        {
            base64Data = Uri.UnescapeDataString(base64Data);

            if (base64Data.Length > maximumBase64Length)
            {
                throw new InvalidDataException(ExternalImageTooLargeMessage);
            }
        }

        byte[] content;

        try
        {
            content = Convert.FromBase64String(base64Data);
        }
        catch (FormatException ex)
        {
            throw new InvalidDataException(InvalidDataImageMessage, ex);
        }

        if (content.LongLength > maxInputBytes)
        {
            throw new InvalidDataException(ExternalImageTooLargeMessage);
        }

        string contentType = ResolveContentType(declaredContentType, content);

        return new AttachedImageDto(fileName, contentType, content);
    }

    private string ResolveContentType(
        MediaTypeHeaderValue? contentType,
        byte[] content)
    {
        return ResolveContentType(contentType?.MediaType, content);
    }

    private string ResolveContentType(
        string? declaredContentType,
        byte[] content)
    {
        if (!string.IsNullOrWhiteSpace(declaredContentType)
            && _signatureValidator.MatchesSignature(
                declaredContentType,
                content))
        {
            return declaredContentType.Trim();
        }

        if (_signatureValidator.TryDetectContentType(content, out string detectedContentType))
        {
            return detectedContentType;
        }

        return !string.IsNullOrWhiteSpace(declaredContentType)
               && declaredContentType.StartsWith(
                   "image/",
                   StringComparison.OrdinalIgnoreCase)
            ? declaredContentType.Trim()
            : UnknownImageContentType;
    }

    private static string BuildFileName(
        Uri imageUri,
        int maximumFileNameCharacters)
    {
        if (string.Equals(
                imageUri.Scheme,
                "data",
                StringComparison.OrdinalIgnoreCase))
        {
            string metadata = imageUri.OriginalString
                .Split(',', 2)[0];
            string contentType = metadata.Length > 5
                ? metadata[5..].Split(';', 2)[0]
                : string.Empty;

            return string.Concat(
                ExternalImageFileName,
                ImageDataTransferFormats.ResolveExtension(contentType));
        }

        string candidate = Uri.UnescapeDataString(
            Path.GetFileName(imageUri.AbsolutePath));

        if (string.IsNullOrWhiteSpace(candidate))
        {
            return ExternalImageFileName;
        }

        return TransferredImageFileName.Sanitize(
            candidate,
            ExternalImageFileName,
            maximumFileNameCharacters);
    }
}
