using System.Text.Json;

using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;

using AtomicArt.Contracts.Generation;

namespace AtomicArt.Api.Generation;

public sealed class MultipartGenerationRequestReader
{
    private static readonly JsonSerializerOptions SerializerOptions =
        new(JsonSerializerDefaults.Web);

    private readonly int _copyBufferSize;
    private readonly int _maximumBoundaryLength;
    private readonly int _maxMetadataBytes;
    private readonly long _maxRequestBytes;

    public MultipartGenerationRequestReader(
        IOptions<GenerationServerOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _copyBufferSize = options.Value.CopyBufferSize;
        _maximumBoundaryLength = options.Value.MaximumBoundaryLength;
        _maxMetadataBytes = options.Value.MaxMetadataBytes;
        _maxRequestBytes = options.Value.MaxRequestBytes;
    }

    public async Task<MultipartGenerationRequest> ReadAsync(
        HttpRequest request,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            return await ReadCoreAsync(request, ct).ConfigureAwait(false);
        }
        catch (GenerationMultipartRequestException)
        {
            throw;
        }
        catch (IOException exception)
        {
            throw new GenerationMultipartRequestException(
                GenerationProtocolErrorCodes.InvalidMultipartRequest,
                "The multipart request stream ended unexpectedly.",
                exception);
        }
    }

    private async Task<MultipartGenerationRequest> ReadCoreAsync(
        HttpRequest request,
        CancellationToken ct)
    {
        string boundary = GetBoundary(request.ContentType);

        if (request.ContentLength > _maxRequestBytes)
        {
            throw CreateInvalidRequestException(
                "The request body exceeds the server safety limit.");
        }

        MultipartReader reader = new(boundary, request.Body);
        MultipartSection? metadataSection = await reader
            .ReadNextSectionAsync(ct)
            .ConfigureAwait(false);

        if (metadataSection is null
            || !HasExpectedPartName(
                metadataSection,
                GenerationApiRoutes.MetadataPartName))
        {
            throw CreateInvalidRequestException(
                "The metadata must be the first part of the multipart request.");
        }

        GenerationRequestMetadataDto metadata =
            await ReadMetadataAsync(metadataSection.Body, ct).ConfigureAwait(false);
        ValidateMetadata(metadata);
        List<TemporaryGenerationAttachmentSource> sources = [];

        try
        {
            for (int index = 0; index < metadata.Attachments.Count; index++)
            {
                MultipartSection? section = await reader
                    .ReadNextSectionAsync(ct)
                    .ConfigureAwait(false);
                GenerationAttachmentMetadataDto descriptor =
                    metadata.Attachments[index];
                string expectedPartName =
                    $"{GenerationApiRoutes.AttachmentPartNamePrefix}{index}";

                if (section is null
                    || descriptor.Index != index
                    || !HasExpectedPartName(section, expectedPartName)
                    || !string.Equals(
                        section.ContentType,
                        descriptor.ContentType,
                        StringComparison.OrdinalIgnoreCase))
                {
                    throw CreateInvalidRequestException(
                        "Attachment order or type does not match the metadata.");
                }

                TemporaryGenerationAttachmentSource source =
                    await CopyAttachmentAsync(section.Body, descriptor, ct)
                        .ConfigureAwait(false);
                sources.Add(source);
            }

            MultipartSection? unexpectedSection = await reader
                .ReadNextSectionAsync(ct)
                .ConfigureAwait(false);

            if (unexpectedSection is not null)
            {
                throw CreateInvalidRequestException(
                    "The multipart request contains undeclared parts.");
            }

            return new MultipartGenerationRequest(metadata, sources.AsReadOnly());
        }
        catch
        {
            foreach (TemporaryGenerationAttachmentSource source in sources)
            {
                await source.DisposeAsync().ConfigureAwait(false);
            }

            throw;
        }
    }

    private void ValidateMetadata(
        GenerationRequestMetadataDto metadata)
    {
        if (metadata.LogicalGenerationId == Guid.Empty
            || metadata.AttemptNumber
                is < GenerationAttemptLimits.MinimumAttemptNumber
                or > GenerationAttemptLimits.MaximumAttemptNumber)
        {
            throw new GenerationMultipartRequestException(
                GenerationProtocolErrorCodes.InvalidAttemptNumber,
                "The logical generation ID or attempt number is invalid.",
                metadata.LogicalGenerationId,
                metadata.AttemptNumber);
        }

        if (string.IsNullOrWhiteSpace(metadata.ModelId)
            || string.IsNullOrWhiteSpace(metadata.Prompt)
            || metadata.Parameters is null
            || metadata.Attachments is null
            || metadata.Attachments.Any(attachment => attachment is null))
        {
            throw new GenerationMultipartRequestException(
                GenerationProtocolErrorCodes.InvalidMultipartRequest,
                "Required generation metadata was not provided.",
                metadata.LogicalGenerationId,
                metadata.AttemptNumber);
        }

        long declaredAttachmentBytes;

        try
        {
            declaredAttachmentBytes = metadata.Attachments
                .Sum(attachment => checked(attachment.ByteLength));
        }
        catch (OverflowException exception)
        {
            throw new GenerationMultipartRequestException(
                GenerationProtocolErrorCodes.InvalidMultipartRequest,
                "The total attachment size is invalid.",
                exception);
        }

        if (declaredAttachmentBytes > _maxRequestBytes)
        {
            throw new GenerationMultipartRequestException(
                GenerationProtocolErrorCodes.InvalidMultipartRequest,
                "The total attachment size exceeds the server safety limit.",
                metadata.LogicalGenerationId,
                metadata.AttemptNumber);
        }
    }

    private async Task<GenerationRequestMetadataDto> ReadMetadataAsync(
        Stream stream,
        CancellationToken ct)
    {
        using MemoryStream buffer = new();
        await CopyWithLimitAsync(
                stream,
                buffer,
                _maxMetadataBytes,
                ct)
            .ConfigureAwait(false);
        buffer.Position = 0L;

        try
        {
            GenerationRequestMetadataDto? metadata =
                await JsonSerializer.DeserializeAsync<GenerationRequestMetadataDto>(
                        buffer,
                        SerializerOptions,
                        ct)
                    .ConfigureAwait(false);

            return metadata ?? throw CreateInvalidRequestException(
                "Generation metadata was not provided.");
        }
        catch (JsonException exception)
        {
            throw new GenerationMultipartRequestException(
                GenerationProtocolErrorCodes.InvalidMultipartRequest,
                "Generation metadata contains invalid JSON.",
                exception);
        }
    }

    private async Task<TemporaryGenerationAttachmentSource> CopyAttachmentAsync(
        Stream source,
        GenerationAttachmentMetadataDto descriptor,
        CancellationToken ct)
    {
        if (descriptor.ByteLength <= 0
            || descriptor.ByteLength > _maxRequestBytes)
        {
            throw CreateInvalidRequestException(
                "The attachment size failed validation.");
        }

        string temporaryPath = Path.Combine(
            Path.GetTempPath(),
            $"atomicart-generation-{Guid.NewGuid():N}.tmp");

        try
        {
            await using FileStream destination = new(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                _copyBufferSize,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            await CopyWithLimitAsync(
                    source,
                    destination,
                    descriptor.ByteLength,
                    ct)
                .ConfigureAwait(false);

            if (destination.Length != descriptor.ByteLength)
            {
                throw CreateInvalidRequestException(
                    "The actual attachment size does not match the metadata.");
            }

            return new TemporaryGenerationAttachmentSource(
                descriptor,
                temporaryPath,
                _copyBufferSize);
        }
        catch
        {
            File.Delete(temporaryPath);
            throw;
        }
    }

    private async Task CopyWithLimitAsync(
        Stream source,
        Stream destination,
        long maximumBytes,
        CancellationToken ct)
    {
        byte[] buffer = new byte[_copyBufferSize];
        long totalBytes = 0L;

        while (true)
        {
            int bytesRead = await source
                .ReadAsync(buffer, ct)
                .ConfigureAwait(false);

            if (bytesRead == 0)
            {
                break;
            }

            totalBytes += bytesRead;

            if (totalBytes > maximumBytes)
            {
                throw CreateInvalidRequestException(
                    "The multipart request part exceeds the allowed size.");
            }

            await destination
                .WriteAsync(buffer.AsMemory(0, bytesRead), ct)
                .ConfigureAwait(false);
        }
    }

    private string GetBoundary(string? contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType)
            || !MediaTypeHeaderValue.TryParse(
                contentType,
                out MediaTypeHeaderValue? mediaType)
            || !string.Equals(
                mediaType.MediaType.Value,
                "multipart/form-data",
                StringComparison.OrdinalIgnoreCase))
        {
            throw CreateInvalidRequestException(
                "Expected a multipart/form-data content type.");
        }

        string boundary = HeaderUtilities.RemoveQuotes(
            mediaType.Boundary).Value ?? string.Empty;

        if (string.IsNullOrWhiteSpace(boundary)
            || boundary.Length > _maximumBoundaryLength)
        {
            throw CreateInvalidRequestException(
                "The multipart boundary is missing or too long.");
        }

        return boundary;
    }

    private bool HasExpectedPartName(
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

    private GenerationMultipartRequestException CreateInvalidRequestException(
        string message)
    {
        return new GenerationMultipartRequestException(
            GenerationProtocolErrorCodes.InvalidMultipartRequest,
            message);
    }
}
