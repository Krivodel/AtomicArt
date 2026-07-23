using System.Buffers;
using System.Buffers.Text;
using System.Net;
using System.Text;
using System.Text.Json;

using AtomicArt.Application.Features.Generation.Interfaces;
using AtomicArt.Application.Features.Generation.Models;
using AtomicArt.Contracts.Generation;

namespace AtomicArt.Infrastructure.Generation.GoogleInteractions;

internal sealed class GoogleInteractionsStreamingContent : HttpContent
{
    private const int InputBufferSize = 49152;
    private const int OutputBufferSize = 65536;
    private const string JsonMediaType = "application/json";
    private const string SystemInstruction =
        "Treat **EVERY user input as an image generation request**. Return **image output only**. DO NOT answer with explanatory text.";

    private readonly StreamingGenerationProviderContext _context;
    private readonly byte[] _prefix;
    private readonly IReadOnlyList<byte[]> _attachmentPrefixes;
    private readonly byte[] _suffix;
    private readonly long _contentLength;

    public GoogleInteractionsStreamingContent(
        StreamingGenerationProviderContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _prefix = CreatePrefix(context);
        _attachmentPrefixes = context.Request.Attachments
            .Select(CreateAttachmentPrefix)
            .ToList();
        _suffix = CreateSuffix(context);
        _contentLength = CalculateContentLength();
        Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(
            JsonMediaType);
        Headers.ContentLength = _contentLength;
    }

    protected override Task SerializeToStreamAsync(
        Stream stream,
        TransportContext? context)
    {
        return SerializeToStreamCoreAsync(stream, CancellationToken.None);
    }

    protected override Task SerializeToStreamAsync(
        Stream stream,
        TransportContext? context,
        CancellationToken ct)
    {
        return SerializeToStreamCoreAsync(stream, ct);
    }

    protected override bool TryComputeLength(out long length)
    {
        length = _contentLength;
        return true;
    }

    private async Task SerializeToStreamCoreAsync(
        Stream destination,
        CancellationToken ct)
    {
        await destination.WriteAsync(_prefix, ct).ConfigureAwait(false);

        for (int index = 0; index < _context.Request.Attachments.Count; index++)
        {
            await destination
                .WriteAsync(_attachmentPrefixes[index], ct)
                .ConfigureAwait(false);
            await WriteAttachmentBase64Async(
                    destination,
                    _context.Request.Attachments[index],
                    ct)
                .ConfigureAwait(false);
            await destination
                .WriteAsync("\"}"u8.ToArray(), ct)
                .ConfigureAwait(false);
        }

        await destination.WriteAsync(_suffix, ct).ConfigureAwait(false);
    }

    private static async Task WriteAttachmentBase64Async(
        Stream destination,
        IGenerationAttachmentSource attachment,
        CancellationToken ct)
    {
        byte[] inputBuffer = ArrayPool<byte>.Shared.Rent(InputBufferSize);
        byte[] outputBuffer = ArrayPool<byte>.Shared.Rent(OutputBufferSize);
        long totalBytesRead = 0L;

        try
        {
            await using Stream source = await attachment
                .OpenReadAsync(ct)
                .ConfigureAwait(false);

            while (true)
            {
                int bytesRead = await FillBufferAsync(
                        source,
                        inputBuffer,
                        InputBufferSize,
                        ct)
                    .ConfigureAwait(false);

                if (bytesRead == 0)
                {
                    break;
                }

                totalBytesRead += bytesRead;
                OperationStatus status = Base64.EncodeToUtf8(
                    inputBuffer.AsSpan(0, bytesRead),
                    outputBuffer,
                    out int consumed,
                    out int written);

                if (status != OperationStatus.Done || consumed != bytesRead)
                {
                    throw new InvalidOperationException(
                        "Attachment Base64 encoding did not consume the complete input block.");
                }

                await destination
                    .WriteAsync(outputBuffer.AsMemory(0, written), ct)
                    .ConfigureAwait(false);
            }

            if (totalBytesRead != attachment.Metadata.ByteLength)
            {
                throw new InvalidDataException(
                    "Attachment length changed after request validation.");
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(inputBuffer);
            ArrayPool<byte>.Shared.Return(outputBuffer);
        }
    }

    private static async Task<int> FillBufferAsync(
        Stream source,
        byte[] buffer,
        int count,
        CancellationToken ct)
    {
        int totalBytesRead = 0;

        while (totalBytesRead < count)
        {
            int bytesRead = await source
                .ReadAsync(buffer.AsMemory(totalBytesRead, count - totalBytesRead), ct)
                .ConfigureAwait(false);

            if (bytesRead == 0)
            {
                break;
            }

            totalBytesRead += bytesRead;
        }

        return totalBytesRead;
    }

    private static byte[] CreatePrefix(
        StreamingGenerationProviderContext context)
    {
        string model = JsonSerializer.Serialize(context.ProviderModelId);
        string prompt = JsonSerializer.Serialize(context.Request.Prompt);
        string prefix =
            $"{{\"model\":{model},\"input\":[{{\"type\":\"text\",\"text\":{prompt}}}";

        return Encoding.UTF8.GetBytes(prefix);
    }

    private static byte[] CreateAttachmentPrefix(
        IGenerationAttachmentSource attachment)
    {
        string contentType = JsonSerializer.Serialize(
            attachment.Metadata.ContentType);
        string prefix =
            $",{{\"type\":\"image\",\"mime_type\":{contentType},\"data\":\"";

        return Encoding.UTF8.GetBytes(prefix);
    }

    private static byte[] CreateSuffix(
        StreamingGenerationProviderContext context)
    {
        Dictionary<string, object> responseFormat = new(StringComparer.Ordinal)
        {
            [GoogleInteractionsContentContract.TypePropertyName] =
                GoogleInteractionsContentContract.ImageType,
            [GoogleInteractionsContentContract.MimeTypePropertyName] =
                GoogleInteractionsImageOutputContract.ContentType,
            ["image_size"] = context.Request.Resolution
        };

        if (!GenerationAspectRatios.IsAuto(context.Request.AspectRatio))
        {
            responseFormat["aspect_ratio"] = context.Request.AspectRatio;
        }

        Dictionary<string, object> generationConfig = new(StringComparer.Ordinal)
        {
            ["temperature"] = context.Request.Temperature
        };

        if (!string.IsNullOrWhiteSpace(context.Request.ThinkingLevel))
        {
            generationConfig["thinking_level"] = context.Request.ThinkingLevel;
        }

        string suffix =
            $"],\"system_instruction\":{JsonSerializer.Serialize(SystemInstruction)},"
            + $"\"generation_config\":{JsonSerializer.Serialize(generationConfig)},"
            + $"\"response_format\":{JsonSerializer.Serialize(responseFormat)},"
            + "\"service_tier\":\"flex\",\"store\":true}";

        return Encoding.UTF8.GetBytes(suffix);
    }

    private long CalculateContentLength()
    {
        long length = _prefix.LongLength + _suffix.LongLength;

        for (int index = 0; index < _context.Request.Attachments.Count; index++)
        {
            long attachmentLength =
                _context.Request.Attachments[index].Metadata.ByteLength;
            long base64Length = checked(((attachmentLength + 2L) / 3L) * 4L);
            length = checked(
                length
                + _attachmentPrefixes[index].LongLength
                + base64Length
                + 2L);
        }

        return length;
    }
}
