using System.Net;
using System.Text;
using System.Text.Json;

using AtomicArt.Application.Features.Generation.Interfaces;
using AtomicArt.Application.Features.Generation.Models;
using AtomicArt.Contracts.Generation;

namespace AtomicArt.Infrastructure.Generation.OpenRouter;

internal sealed class OpenRouterImageRequestContent : HttpContent
{
    private const string OpenAiGptImage2ModelId = "openai/gpt-image-2";
    private const int GptImageSizeAlignment = 16;
    private const int OneKImageShortEdge = 1024;
    private const int TwoKImageShortEdge = 2048;
    private const int FourKImageShortEdge = 4096;

    private readonly StreamingGenerationProviderContext _context;
    private readonly byte[] _prefix;
    private readonly IReadOnlyList<byte[]> _attachmentPrefixes;
    private readonly byte[] _suffix = "]}"u8.ToArray();
    private readonly long _contentLength;

    public OpenRouterImageRequestContent(
        StreamingGenerationProviderContext context,
        string? flexProviderTag = null)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _prefix = CreatePrefix(context, flexProviderTag);
        _attachmentPrefixes = context.Request.Attachments
            .Select((attachment, index) => CreateAttachmentPrefix(attachment, index))
            .ToList();
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

    private static byte[] CreatePrefix(
        StreamingGenerationProviderContext context,
        string? flexProviderTag)
    {
        StringBuilder builder = new("{\"model\":");
        builder.Append(JsonSerializer.Serialize(context.ProviderModelId));
        builder.Append(",\"prompt\":");
        builder.Append(JsonSerializer.Serialize(context.Request.Prompt));
        builder.Append(",\"n\":1,\"output_format\":\"png\"");

        bool isGptImage2 = string.Equals(
            context.ProviderModelId,
            OpenAiGptImage2ModelId,
            StringComparison.Ordinal);

        if (isGptImage2)
        {
            builder.Append(",\"size\":");
            string size = GenerationAspectRatios.IsAuto(context.Request.AspectRatio)
                ? context.Request.Resolution
                : CreateGptImageSize(
                    context.Request.Resolution,
                    context.Request.AspectRatio);
            builder.Append(JsonSerializer.Serialize(size));
        }
        else
        {
            builder.Append(",\"resolution\":");
            builder.Append(JsonSerializer.Serialize(context.Request.Resolution));
        }

        if (isGptImage2 || !GenerationAspectRatios.IsAuto(context.Request.AspectRatio))
        {
            builder.Append(",\"aspect_ratio\":");
            builder.Append(JsonSerializer.Serialize(context.Request.AspectRatio));
        }

        if (!string.IsNullOrWhiteSpace(flexProviderTag))
        {
            builder.Append(",\"service_tier\":\"flex\"");
            builder.Append(",\"provider\":{\"only\":[");
            builder.Append(JsonSerializer.Serialize(flexProviderTag));
            builder.Append("],\"allow_fallbacks\":false}");
        }

        builder.Append(",\"input_references\":[");
        return Encoding.UTF8.GetBytes(builder.ToString());
    }

    private static string CreateGptImageSize(string resolution, string aspectRatio)
    {
        int shortEdge = resolution switch
        {
            "1K" => OneKImageShortEdge,
            "2K" => TwoKImageShortEdge,
            "4K" => FourKImageShortEdge,
            _ => throw new InvalidOperationException(
                $"Unsupported GPT Image resolution '{resolution}'.")
        };

        if (string.Equals(aspectRatio, "auto", StringComparison.Ordinal))
        {
            return $"{shortEdge}x{shortEdge}";
        }

        string[] ratioComponents = aspectRatio.Split(':', StringSplitOptions.TrimEntries);

        if (ratioComponents.Length != 2
            || !int.TryParse(ratioComponents[0], out int widthRatio)
            || !int.TryParse(ratioComponents[1], out int heightRatio)
            || widthRatio <= 0
            || heightRatio <= 0)
        {
            throw new InvalidOperationException(
                $"Invalid GPT Image aspect ratio '{aspectRatio}'.");
        }

        if (widthRatio >= heightRatio)
        {
            int width = RoundUpToAlignment(shortEdge * widthRatio / (double)heightRatio);
            return $"{width}x{shortEdge}";
        }

        int height = RoundUpToAlignment(shortEdge * heightRatio / (double)widthRatio);
        return $"{shortEdge}x{height}";
    }

    private static int RoundUpToAlignment(double value)
    {
        return checked((int)Math.Ceiling(value / GptImageSizeAlignment) * GptImageSizeAlignment);
    }

    private static byte[] CreateAttachmentPrefix(
        IGenerationAttachmentSource attachment,
        int index)
    {
        ArgumentNullException.ThrowIfNull(attachment);

        string separator = index == 0 ? string.Empty : ",";
        string dataUrlPrefix =
            $"{separator}{{\"type\":\"image_url\",\"image_url\":{{\"url\":\"data:{attachment.Metadata.ContentType};base64,";
        return Encoding.UTF8.GetBytes(dataUrlPrefix);
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
