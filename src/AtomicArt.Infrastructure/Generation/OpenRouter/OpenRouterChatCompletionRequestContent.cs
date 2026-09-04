using System.Net;
using System.Text;
using System.Text.Json;

using AtomicArt.Application.Features.Generation.Interfaces;
using AtomicArt.Application.Features.Generation.Models;
using AtomicArt.Contracts.Generation;
using AtomicArt.Infrastructure.Generation.GoogleInteractions;

namespace AtomicArt.Infrastructure.Generation.OpenRouter;

internal sealed class OpenRouterChatCompletionRequestContent : HttpContent
{
    private readonly StreamingGenerationProviderContext _context;
    private readonly byte[] _prefix;
    private readonly IReadOnlyList<byte[]> _attachmentPrefixes;
    private readonly byte[] _suffix;
    private readonly long _contentLength;

    public OpenRouterChatCompletionRequestContent(
        StreamingGenerationProviderContext context,
        string? providerTag)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _prefix = CreatePrefix(context);
        _attachmentPrefixes = context.Request.Attachments
            .Select((attachment, index) => CreateAttachmentPrefix(attachment, index))
            .ToList();
        _suffix = CreateSuffix(context, providerTag);
        _contentLength = CalculateContentLength();
        Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
        Headers.ContentLength = _contentLength;
    }

    protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context)
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

    private async Task SerializeToStreamCoreAsync(Stream destination, CancellationToken ct)
    {
        await destination.WriteAsync(_prefix, ct).ConfigureAwait(false);

        for (int index = 0; index < _context.Request.Attachments.Count; index++)
        {
            await destination.WriteAsync(_attachmentPrefixes[index], ct).ConfigureAwait(false);
            await OpenRouterAttachmentBase64Writer.WriteAsync(
                    destination,
                    _context.Request.Attachments[index],
                    ct)
                .ConfigureAwait(false);
            await destination.WriteAsync("\"}}"u8.ToArray(), ct).ConfigureAwait(false);
        }

        await destination.WriteAsync(_suffix, ct).ConfigureAwait(false);
    }

    private static byte[] CreatePrefix(StreamingGenerationProviderContext context)
    {
        StringBuilder builder = new("{\"model\":");
        builder.Append(JsonSerializer.Serialize(context.ProviderModelId));
        builder.Append(",\"messages\":[{\"role\":\"system\",\"content\":");
        builder.Append(JsonSerializer.Serialize(GoogleInteractionsImageOutputContract.SystemInstruction));
        builder.Append("},{\"role\":\"user\",\"content\":[{\"type\":\"text\",\"text\":");
        builder.Append(JsonSerializer.Serialize(context.Request.Prompt));
        builder.Append("}");
        return Encoding.UTF8.GetBytes(builder.ToString());
    }

    private static byte[] CreateAttachmentPrefix(
        IGenerationAttachmentSource attachment,
        int index)
    {
        ArgumentNullException.ThrowIfNull(attachment);

        string prefix = $",{{\"type\":\"image_url\",\"image_url\":{{\"url\":\"data:{attachment.Metadata.ContentType};base64,";
        return Encoding.UTF8.GetBytes(prefix);
    }

    private static byte[] CreateSuffix(
        StreamingGenerationProviderContext context,
        string? providerTag)
    {
        StringBuilder builder = new("]}],\"modalities\":[\"image\",\"text\"],\"stream\":false,\"image_config\":{\"image_size\":");
        builder.Append(JsonSerializer.Serialize(context.Request.Resolution));

        if (!GenerationAspectRatios.IsAuto(context.Request.AspectRatio))
        {
            builder.Append(",\"aspect_ratio\":");
            builder.Append(JsonSerializer.Serialize(context.Request.AspectRatio));
        }

        builder.Append("},\"temperature\":");
        builder.Append(JsonSerializer.Serialize(context.Request.Temperature));

        if (!string.IsNullOrWhiteSpace(context.Request.ThinkingLevel))
        {
            builder.Append(",\"reasoning_effort\":");
            builder.Append(JsonSerializer.Serialize(context.Request.ThinkingLevel));
        }

        builder.Append(",\"service_tier\":\"flex\"");

        if (!string.IsNullOrWhiteSpace(providerTag))
        {
            builder.Append(",\"provider\":{\"only\":[");
            builder.Append(JsonSerializer.Serialize(providerTag));
            builder.Append("],\"allow_fallbacks\":false}");
        }

        builder.Append('}');
        return Encoding.UTF8.GetBytes(builder.ToString());
    }

    private long CalculateContentLength()
    {
        long length = _prefix.LongLength + _suffix.LongLength;

        for (int index = 0; index < _context.Request.Attachments.Count; index++)
        {
            long attachmentLength = _context.Request.Attachments[index].Metadata.ByteLength;
            long base64Length = OpenRouterAttachmentBase64Writer.GetEncodedLength(attachmentLength);
            length = checked(length + _attachmentPrefixes[index].LongLength + base64Length + 3L);
        }

        return length;
    }
}
