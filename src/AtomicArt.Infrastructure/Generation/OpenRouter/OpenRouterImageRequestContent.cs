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
    private const string OpenAiGptImage25SunburstModelId = "openai/gpt-image-2.5-sunburst";
    private const string OpenAiGptImage25FlareModelId = "openai/gpt-image-2.5-flare";
    private const int GptImageSizeAlignment = 16;
    private const int GptImageMinimumEdge = 256;
    private const int GptImageMaximumEdge = 3840;
    private const int OneKImagePixelBudget = 1024 * 1024;
    private const int TwoKImagePixelBudget = 2560 * 1440;
    private const int FourKImagePixelBudget = 3840 * 2160;

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

        bool isGptImage2 = IsGptImageModel(context.ProviderModelId);

        if (isGptImage2)
        {
            builder.Append(",\"size\":");
            string size = CreateGptImageSize(
                context.Request.Resolution,
                context.Request.AspectRatio);
            builder.Append(JsonSerializer.Serialize(size));
        }
        else if (!isGptImage2)
        {
            builder.Append(",\"resolution\":");
            builder.Append(JsonSerializer.Serialize(context.Request.Resolution));
        }

        if (!isGptImage2 && !GenerationAspectRatios.IsAuto(context.Request.AspectRatio))
        {
            builder.Append(",\"aspect_ratio\":");
            builder.Append(JsonSerializer.Serialize(context.Request.AspectRatio));
        }

        if (context.Request.Parameters.TryGetValue(
                GenerationParameterNames.Quality,
                out JsonElement quality))
        {
            builder.Append(",\"quality\":");
            builder.Append(quality.GetRawText());
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

    private static bool IsGptImageModel(string providerModelId)
    {
        return string.Equals(providerModelId, OpenAiGptImage2ModelId, StringComparison.Ordinal)
            || string.Equals(providerModelId, OpenAiGptImage25SunburstModelId, StringComparison.Ordinal)
            || string.Equals(providerModelId, OpenAiGptImage25FlareModelId, StringComparison.Ordinal);
    }

    private static string CreateGptImageSize(string resolution, string aspectRatio)
    {
        int pixelBudget = resolution switch
        {
            "1K" => OneKImagePixelBudget,
            "2K" => TwoKImagePixelBudget,
            "4K" => FourKImagePixelBudget,
            _ => throw new InvalidOperationException(
                $"Unsupported GPT Image resolution '{resolution}'.")
        };

        (int widthRatio, int heightRatio) = GenerationAspectRatios.IsAuto(aspectRatio)
            ? (1, 1)
            : ParseAspectRatio(aspectRatio);

        double width = Math.Sqrt(pixelBudget * widthRatio / (double)heightRatio);
        double height = Math.Sqrt(pixelBudget * heightRatio / (double)widthRatio);
        double scale = Math.Min(1d, GptImageMaximumEdge / Math.Max(width, height));

        return $"{RoundImageEdge(width * scale)}x{RoundImageEdge(height * scale)}";
    }

    private static (int Width, int Height) ParseAspectRatio(string aspectRatio)
    {
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

        return (widthRatio, heightRatio);
    }

    private static int RoundImageEdge(double value)
    {
        int rounded = checked((int)Math.Round(
            value / GptImageSizeAlignment,
            MidpointRounding.AwayFromZero) * GptImageSizeAlignment);
        return Math.Clamp(rounded, GptImageMinimumEdge, GptImageMaximumEdge);
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
