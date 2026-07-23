using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using Microsoft.Net.Http.Headers;

using AtomicArt.Contracts.Generation;
using AtomicArt.Desktop.Services.Generation;
using ContentDispositionHeaderValue =
    Microsoft.Net.Http.Headers.ContentDispositionHeaderValue;
using MediaTypeHeaderValue =
    Microsoft.Net.Http.Headers.MediaTypeHeaderValue;

namespace AtomicArt.Desktop.Services;

public sealed class ImageGenerationApiClient
    : AtomicArtApiClient, IImageGenerationApiClient
{
    private const int MaximumMetadataBytes = 256 * 1024;
    private const string ProviderIdHeaderName = "X-AtomicArt-Provider-Id";

    private static readonly JsonSerializerOptions SerializerOptions =
        new(JsonSerializerDefaults.Web);

    private readonly IGenerationStreamingResultStore _resultStore;
    private readonly ProviderResponseImageDecoderRegistry _decoderRegistry;

    public ImageGenerationApiClient(
        HttpClient httpClient,
        IApiEndpointService apiEndpointService,
        IGenerationStreamingResultStore resultStore,
        ProviderResponseImageDecoderRegistry decoderRegistry,
        ILogger<ImageGenerationApiClient> logger)
        : base(httpClient, apiEndpointService, logger)
    {
        _resultStore = resultStore
            ?? throw new ArgumentNullException(nameof(resultStore));
        _decoderRegistry = decoderRegistry
            ?? throw new ArgumentNullException(nameof(decoderRegistry));
    }

    public async Task<GenerationBatchDto> CreateGenerationAsync(
        ImageGenerationRequestDto request,
        Guid logicalGenerationId,
        int attemptNumber,
        string providerCredential,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentOutOfRangeException.ThrowIfLessThan(attemptNumber, 1);

        Stopwatch stopwatch = Stopwatch.StartNew();
        Uri requestUri =
            ApiEndpointService.CreateRequestUri(GenerationApiRoutes.Generations);
        bool hasProviderCredential =
            !string.IsNullOrWhiteSpace(providerCredential);

        if (hasProviderCredential)
        {
            EnsureTrustedProviderCredentialTarget(requestUri);
        }

        using HttpRequestMessage requestMessage = new(
            HttpMethod.Post,
            requestUri);
        requestMessage.Content = CreateMultipartContent(
            request,
            logicalGenerationId,
            attemptNumber);

        if (hasProviderCredential)
        {
            requestMessage.Headers.TryAddWithoutValidation(
                GenerationApiRoutes.ProviderApiKeyHeaderName,
                providerCredential);
        }

        using HttpResponseMessage response = await HttpClient
            .SendAsync(
                requestMessage,
                HttpCompletionOption.ResponseHeadersRead,
                ct)
            .ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            GenerationProblemDetails problemDetails =
                await ReadProblemDetailsAsync(response.Content, ct)
                    .ConfigureAwait(false);

            Logger.LogWarning(
                "Image generation attempt {AttemptNumber} for logical generation {LogicalGenerationId} returned HTTP {StatusCode} with safe error code {SafeErrorCode} after {ElapsedMilliseconds} ms.",
                attemptNumber,
                logicalGenerationId,
                (int)response.StatusCode,
                problemDetails.SafeErrorCode,
                stopwatch.ElapsedMilliseconds);

            throw new GenerationAttemptException(
                "Generation API rejected the attempt.",
                problemDetails.SafeErrorCode,
                problemDetails.Retryable);
        }

        try
        {
            GenerationBatchDto batch = await ReadMultipartResponseAsync(
                    response,
                    request,
                    logicalGenerationId,
                    attemptNumber,
                    ct)
                .ConfigureAwait(false);

            Logger.LogInformation(
                "Image generation attempt {AttemptNumber} for logical generation {LogicalGenerationId} completed as batch {BatchId} after {ElapsedMilliseconds} ms.",
                attemptNumber,
                logicalGenerationId,
                batch.BatchId,
                stopwatch.ElapsedMilliseconds);

            return batch;
        }
        catch (GenerationAttemptException)
        {
            throw;
        }
        catch (Exception exception) when (exception is IOException
            or HttpRequestException
            or JsonException)
        {
            throw new GenerationAttemptException(
                "Streaming generation response was interrupted or malformed.",
                GenerationProtocolErrorCodes.TransportInterrupted,
                false,
                exception);
        }
    }

    private async Task<GenerationBatchDto> ReadMultipartResponseAsync(
        HttpResponseMessage response,
        ImageGenerationRequestDto request,
        Guid logicalGenerationId,
        int attemptNumber,
        CancellationToken ct)
    {
        string boundary = GetResponseBoundary(response.Content.Headers.ContentType);
        await using Stream responseStream = await response.Content
            .ReadAsStreamAsync(ct)
            .ConfigureAwait(false);
        MultipartReader reader = new(boundary, responseStream);
        MultipartSection? providerSection = await reader
            .ReadNextSectionAsync(ct)
            .ConfigureAwait(false);

        if (providerSection is null
            || !HasExpectedPartName(
                providerSection,
                GenerationApiRoutes.ProviderResponsePartName))
        {
            throw new InvalidDataException(
                "Streaming response does not start with the provider response.");
        }

        string providerId = GetRequiredHeader(
            providerSection,
            ProviderIdHeaderName);
        string providerContentType = providerSection.ContentType
            ?? throw new InvalidDataException(
                "Provider response content type is missing.");
        IProviderResponseImageDecoder decoder = _decoderRegistry.GetRequired(
            providerId,
            providerContentType);
        await using GenerationTemporaryResult temporaryResult =
            _resultStore.CreateTemporaryResult();
        ProviderResponseImageDecodeResult decodeResult = new();
        await decoder
            .DecodeAsync(
                providerSection.Body,
                temporaryResult.Stream,
                decodeResult,
                ct)
            .ConfigureAwait(false);
        MultipartSection? metadataSection = await reader
            .ReadNextSectionAsync(ct)
            .ConfigureAwait(false);

        if (metadataSection is null
            || !HasExpectedPartName(
                metadataSection,
                GenerationApiRoutes.GenerationMetadataPartName))
        {
            throw new InvalidDataException(
                "Streaming response ended without generation metadata.");
        }

        GenerationAttemptMetadataDto metadata =
            await ReadMetadataAsync(metadataSection.Body, ct)
                .ConfigureAwait(false);
        MultipartSection? unexpectedSection = await reader
            .ReadNextSectionAsync(ct)
            .ConfigureAwait(false);

        if (unexpectedSection is not null
            || metadata.LogicalGenerationId != logicalGenerationId
            || metadata.AttemptNumber != attemptNumber)
        {
            throw new InvalidDataException(
                "Streaming response metadata does not match the request.");
        }

        if (metadata.Status != GenerationItemStatus.Generated)
        {
            throw new GenerationAttemptException(
                "The provider response did not contain a successful generation.",
                metadata.SafeErrorCode,
                metadata.Retryable);
        }

        if (metadata.ResultCount != 1
            || metadata.ContentTypes.Count != 1
            || !decodeResult.HasImage)
        {
            throw new InvalidDataException(
                "Generation attempt returned an unexpected result count.");
        }

        string contentType = metadata.ContentTypes[0];
        await temporaryResult
            .CommitAsync(
                metadata.BatchId,
                metadata.ItemId,
                contentType,
                ct)
            .ConfigureAwait(false);
        string resultPath = temporaryResult.FinalPath
            ?? throw new InvalidOperationException(
                "Committed generation result path is missing.");
        DateTime createdAtUtc =
            metadata.CompletedAtUtc - metadata.GenerationDuration;
        GenerationItemDto item = new(
            metadata.ItemId,
            request.ModelId,
            metadata.ModelDisplayName,
            request.Prompt,
            request.AspectRatio,
            request.Resolution,
            createdAtUtc,
            GenerationItemStatus.Generated,
            resultPath,
            null,
            metadata.CompletedAtUtc,
            metadata.GenerationDuration,
            metadata.Price,
            metadata.Usage);

        return new GenerationBatchDto(
            metadata.BatchId,
            new List<GenerationItemDto>
            {
                item
            });
    }

    private static MultipartFormDataContent CreateMultipartContent(
        ImageGenerationRequestDto request,
        Guid logicalGenerationId,
        int attemptNumber)
    {
        Dictionary<string, JsonElement> parameters =
            new Dictionary<string, JsonElement>(StringComparer.Ordinal)
            {
                [GenerationParameterNames.Temperature] =
                    JsonSerializer.SerializeToElement(request.Temperature),
                [GenerationParameterNames.AspectRatio] =
                    JsonSerializer.SerializeToElement(request.AspectRatio),
                [GenerationParameterNames.Resolution] =
                    JsonSerializer.SerializeToElement(request.Resolution)
            };

        if (!string.IsNullOrWhiteSpace(request.ThinkingLevel))
        {
            parameters[GenerationParameterNames.ThinkingLevel] =
                JsonSerializer.SerializeToElement(request.ThinkingLevel);
        }

        List<GenerationAttachmentMetadataDto> attachmentMetadata =
            request.AttachedImages
                .Select((attachment, index) =>
                    new GenerationAttachmentMetadataDto(
                        index,
                        attachment.FileName,
                        attachment.ContentType,
                        attachment.Content.LongLength))
                .ToList();
        GenerationRequestMetadataDto metadata = new(
            logicalGenerationId,
            attemptNumber,
            request.ModelId,
            request.Prompt,
            parameters,
            attachmentMetadata);
        MultipartFormDataContent multipart = new();
        string metadataJson = JsonSerializer.Serialize(
            metadata,
            SerializerOptions);
        StringContent metadataContent = new(
            metadataJson,
            Encoding.UTF8,
            "application/json");
        multipart.Add(metadataContent, GenerationApiRoutes.MetadataPartName);

        for (int index = 0; index < request.AttachedImages.Count; index++)
        {
            AttachedImageDto attachment = request.AttachedImages[index];
            MemoryStream stream = new(attachment.Content, writable: false);
            StreamContent content = new(stream);
            content.Headers.ContentType =
                new System.Net.Http.Headers.MediaTypeHeaderValue(
                    attachment.ContentType);
            multipart.Add(
                content,
                $"{GenerationApiRoutes.AttachmentPartNamePrefix}{index}",
                attachment.FileName);
        }

        return multipart;
    }

    private static async Task<GenerationAttemptMetadataDto> ReadMetadataAsync(
        Stream source,
        CancellationToken ct)
    {
        using MemoryStream buffer = new();
        byte[] copyBuffer = new byte[8192];
        int totalBytes = 0;

        while (true)
        {
            int bytesRead = await source
                .ReadAsync(copyBuffer, ct)
                .ConfigureAwait(false);

            if (bytesRead == 0)
            {
                break;
            }

            totalBytes += bytesRead;

            if (totalBytes > MaximumMetadataBytes)
            {
                throw new InvalidDataException(
                    "Generation response metadata exceeds its limit.");
            }

            await buffer
                .WriteAsync(copyBuffer.AsMemory(0, bytesRead), ct)
                .ConfigureAwait(false);
        }

        buffer.Position = 0L;
        GenerationAttemptMetadataDto? metadata =
            await JsonSerializer.DeserializeAsync<GenerationAttemptMetadataDto>(
                    buffer,
                    SerializerOptions,
                    ct)
                .ConfigureAwait(false);

        return metadata ?? throw new InvalidDataException(
            "Generation response metadata is empty.");
    }

    private static async Task<GenerationProblemDetails> ReadProblemDetailsAsync(
        HttpContent content,
        CancellationToken ct)
    {
        await using Stream stream = await content
            .ReadAsStreamAsync(ct)
            .ConfigureAwait(false);
        using MemoryStream buffer = new();
        byte[] bytes = new byte[16385];
        int totalBytes = 0;

        while (totalBytes < bytes.Length)
        {
            int bytesRead = await stream
                .ReadAsync(bytes.AsMemory(totalBytes), ct)
                .ConfigureAwait(false);

            if (bytesRead == 0)
            {
                break;
            }

            totalBytes += bytesRead;
        }

        if (totalBytes == 0 || totalBytes == bytes.Length)
        {
            return new GenerationProblemDetails(null, false);
        }

        buffer.Write(bytes, 0, totalBytes);
        buffer.Position = 0L;

        try
        {
            using JsonDocument document = await JsonDocument.ParseAsync(
                    buffer,
                    cancellationToken: ct)
                .ConfigureAwait(false);
            JsonElement root = document.RootElement;
            string? safeErrorCode = root.TryGetProperty(
                    GenerationApiRoutes.ProblemDetailsErrorCodeExtensionName,
                    out JsonElement codeElement)
                && codeElement.ValueKind == JsonValueKind.String
                    ? codeElement.GetString()
                    : null;
            bool retryable = root.TryGetProperty(
                    GenerationApiRoutes.ProblemDetailsRetryableExtensionName,
                    out JsonElement retryableElement)
                && retryableElement.ValueKind is JsonValueKind.True
                    or JsonValueKind.False
                && retryableElement.GetBoolean();

            return new GenerationProblemDetails(safeErrorCode, retryable);
        }
        catch (JsonException)
        {
            return new GenerationProblemDetails(null, false);
        }
    }

    private static string GetResponseBoundary(
        System.Net.Http.Headers.MediaTypeHeaderValue? contentType)
    {
        if (contentType is null
            || !string.Equals(
                contentType.MediaType,
                "multipart/mixed",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                "Generation API returned an unexpected content type.");
        }

        System.Net.Http.Headers.NameValueHeaderValue? boundaryParameter =
            contentType.Parameters.FirstOrDefault(parameter =>
                string.Equals(
                    parameter.Name,
                    "boundary",
                    StringComparison.OrdinalIgnoreCase));
        string boundary = boundaryParameter?.Value?.Trim('"')
            ?? string.Empty;

        return !string.IsNullOrWhiteSpace(boundary)
            ? boundary
            : throw new InvalidDataException(
                "Generation multipart boundary is missing.");
    }

    private static bool HasExpectedPartName(
        MultipartSection section,
        string expectedName)
    {
        if (!ContentDispositionHeaderValue.TryParse(
                section.ContentDisposition,
                out ContentDispositionHeaderValue? contentDisposition))
        {
            return false;
        }

        string partName = HeaderUtilities.RemoveQuotes(
            contentDisposition.Name).Value ?? string.Empty;

        return string.Equals(partName, expectedName, StringComparison.Ordinal);
    }

    private static string GetRequiredHeader(
        MultipartSection section,
        string headerName)
    {
        string value = section.Headers?[headerName].FirstOrDefault()
            ?? string.Empty;

        return !string.IsNullOrWhiteSpace(value)
            ? value
            : throw new InvalidDataException(
                "Provider response identifier is missing.");
    }

    private static void EnsureTrustedProviderCredentialTarget(Uri targetUri)
    {
        if (targetUri.Scheme == Uri.UriSchemeHttps
            || targetUri.Scheme == Uri.UriSchemeHttp)
        {
            return;
        }

        throw new InvalidOperationException(
            "Provider credential can be sent only to HTTP or HTTPS API endpoint.");
    }

    private sealed record GenerationProblemDetails(
        string? SafeErrorCode,
        bool Retryable);
}
