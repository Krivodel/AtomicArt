using System.Text.Json;
using System.Text.Json.Nodes;

using AtomicArt.Application.Features.Generation.Models;
using AtomicArt.Contracts.Generation;

namespace AtomicArt.Infrastructure.Generation.GoogleInteractions;

internal sealed class GoogleInteractionsRequestBuilder
{
    private const string ImageModality = "image";
    private const string SystemInstruction =
        "Treat **EVERY user input as an image generation request**. Return **image output only**. DO NOT answer with explanatory text.";

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public string Build(ImageGenerationContentProviderContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(context.Request);
        ArgumentException.ThrowIfNullOrWhiteSpace(context.ProviderModelId);

        JsonArray content = [CreateTextContent(context.Request.Prompt)];

        AddAttachedImages(content, context.Request.AttachedImages);

        JsonObject responseFormat = new()
        {
            ["type"] = ImageModality,
            ["mime_type"] = GoogleInteractionsImageOutputContract.ContentType,
            ["image_size"] = context.Request.Resolution
        };

        if (!GenerationAspectRatios.IsAuto(context.Request.AspectRatio))
        {
            responseFormat["aspect_ratio"] = context.Request.AspectRatio;
        }

        JsonObject generationConfig = new()
        {
            ["temperature"] = context.Request.Temperature
        };

        if (!string.IsNullOrWhiteSpace(context.Request.ThinkingLevel))
        {
            generationConfig["thinking_level"] = context.Request.ThinkingLevel;
        }

        JsonObject requestJson = new()
        {
            ["model"] = context.ProviderModelId,
            ["input"] = content,
            ["system_instruction"] = SystemInstruction,
            ["generation_config"] = generationConfig,
            ["response_format"] = responseFormat,
            ["service_tier"] = "flex",
            ["store"] = true
        };

        return requestJson.ToJsonString(SerializerOptions);
    }

    private static JsonObject CreateTextContent(string prompt)
    {
        return new JsonObject
        {
            ["type"] = "text",
            ["text"] = prompt
        };
    }

    private static void AddAttachedImages(
        JsonArray content,
        IReadOnlyList<AttachedImageDto>? attachedImages)
    {
        if (attachedImages is null)
        {
            return;
        }

        foreach (AttachedImageDto attachedImage in attachedImages)
        {
            content.Add(CreateImageContent(attachedImage));
        }
    }

    private static JsonObject CreateImageContent(AttachedImageDto attachedImage)
    {
        return new JsonObject
        {
            ["type"] = "image",
            ["mime_type"] = attachedImage.ContentType,
            ["data"] = Convert.ToBase64String(attachedImage.Content)
        };
    }
}
