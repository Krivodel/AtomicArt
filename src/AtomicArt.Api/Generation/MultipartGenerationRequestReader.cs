using System.Text.Json;

using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Net.Http.Headers;

using AtomicArt.Contracts.Generation;

namespace AtomicArt.Api.Generation;

public sealed class MultipartGenerationRequestReader
{
    private const int MaximumBoundaryLength = 256;
    private const int MaximumMetadataBytes = 256 * 1024;
    private const long GlobalMaximumRequestBytes = 1024L * 1024L * 1024L;
    private const int CopyBufferSize = 65536;

    private static readonly JsonSerializerOptions SerializerOptions =
        new(JsonSerializerDefaults.Web);

    public async Task<MultipartGenerationRequest> ReadAsync(
        HttpRequest request,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            return await ReadCoreAsync(request, ct).ConfigureAwait(false);
        }
        catch (IOException exception)
        {
            throw new GenerationMultipartRequestException(
                GenerationProtocolErrorCodes.InvalidMultipartRequest,
                "Поток multipart-запроса неожиданно завершился.",
                exception);
        }
    }

    private static async Task<MultipartGenerationRequest> ReadCoreAsync(
        HttpRequest request,
        CancellationToken ct)
    {
        string boundary = GetBoundary(request.ContentType);

        if (request.ContentLength > GlobalMaximumRequestBytes)
        {
            throw CreateInvalidRequestException(
                "Тело запроса превышает аварийный предел.");
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
                "Первой частью multipart-запроса должны быть метаданные.");
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
                        "Порядок или тип вложений не совпадает с метаданными.");
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
                    "Multipart-запрос содержит незаявленные части.");
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

    private static void ValidateMetadata(
        GenerationRequestMetadataDto metadata)
    {
        if (metadata.LogicalGenerationId == Guid.Empty
            || metadata.AttemptNumber
                is < GenerationAttemptLimits.MinimumAttemptNumber
                or > GenerationAttemptLimits.MaximumAttemptNumber)
        {
            throw new GenerationMultipartRequestException(
                GenerationProtocolErrorCodes.InvalidAttemptNumber,
                "Идентификатор логической генерации или номер попытки некорректен.",
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
                "Обязательные метаданные генерации не переданы.",
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
                "Суммарный размер вложений некорректен.",
                exception);
        }

        if (declaredAttachmentBytes > GlobalMaximumRequestBytes)
        {
            throw new GenerationMultipartRequestException(
                GenerationProtocolErrorCodes.InvalidMultipartRequest,
                "Суммарный размер вложений превышает аварийный предел.",
                metadata.LogicalGenerationId,
                metadata.AttemptNumber);
        }
    }

    private static async Task<GenerationRequestMetadataDto> ReadMetadataAsync(
        Stream stream,
        CancellationToken ct)
    {
        using MemoryStream buffer = new();
        await CopyWithLimitAsync(
                stream,
                buffer,
                MaximumMetadataBytes,
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
                "Метаданные генерации не переданы.");
        }
        catch (JsonException exception)
        {
            throw new GenerationMultipartRequestException(
                GenerationProtocolErrorCodes.InvalidMultipartRequest,
                "Метаданные генерации содержат некорректный JSON.",
                exception);
        }
    }

    private static async Task<TemporaryGenerationAttachmentSource> CopyAttachmentAsync(
        Stream source,
        GenerationAttachmentMetadataDto descriptor,
        CancellationToken ct)
    {
        if (descriptor.ByteLength <= 0
            || descriptor.ByteLength > GlobalMaximumRequestBytes)
        {
            throw CreateInvalidRequestException(
                "Размер вложения не прошёл проверку.");
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
                CopyBufferSize,
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
                    "Фактический размер вложения не совпадает с метаданными.");
            }

            return new TemporaryGenerationAttachmentSource(
                descriptor,
                temporaryPath);
        }
        catch
        {
            File.Delete(temporaryPath);
            throw;
        }
    }

    private static async Task CopyWithLimitAsync(
        Stream source,
        Stream destination,
        long maximumBytes,
        CancellationToken ct)
    {
        byte[] buffer = new byte[CopyBufferSize];
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
                    "Часть multipart-запроса превышает заявленный размер.");
            }

            await destination
                .WriteAsync(buffer.AsMemory(0, bytesRead), ct)
                .ConfigureAwait(false);
        }
    }

    private static string GetBoundary(string? contentType)
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
                "Ожидается тип содержимого multipart/form-data.");
        }

        string boundary = HeaderUtilities.RemoveQuotes(
            mediaType.Boundary).Value ?? string.Empty;

        if (string.IsNullOrWhiteSpace(boundary)
            || boundary.Length > MaximumBoundaryLength)
        {
            throw CreateInvalidRequestException(
                "Граница multipart-запроса отсутствует или слишком длинна.");
        }

        return boundary;
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

    private static GenerationMultipartRequestException CreateInvalidRequestException(
        string message)
    {
        return new GenerationMultipartRequestException(
            GenerationProtocolErrorCodes.InvalidMultipartRequest,
            message);
    }
}
