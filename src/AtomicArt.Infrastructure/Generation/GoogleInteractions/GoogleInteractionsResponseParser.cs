using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

using AtomicArt.Application.Features.Generation.Models;
using AtomicArt.Application.Features.Generation.Services;
using AtomicArt.Contracts.Generation;

namespace AtomicArt.Infrastructure.Generation.GoogleInteractions;

internal sealed class GoogleInteractionsResponseParser
{
    private const string ContentPropertyName = "content";
    private const string IncompleteUsageMessage = "The generation provider returned incomplete usage data.";
    private const string InvalidStatusMessage = "The generation provider returned an invalid status.";
    private const string ModelOutputCamelCasePropertyName = "modelOutput";
    private const string ModelOutputSnakeCasePropertyName = "model_output";
    private const string NoImageCategory = "no_image";
    private const string OutputImageCamelCasePropertyName = "outputImage";
    private const string OutputImageSnakeCasePropertyName = "output_image";
    private const string OutputImagesCamelCasePropertyName = "outputImages";
    private const string OutputImagesSnakeCasePropertyName = "output_images";
    private const string OutputPropertyName = "output";
    private const string StatePropertyName = "state";
    private const string StatusPropertyName = "status";
    private const string StepsPropertyName = "steps";
    private const string TextOnlyCategory = "text_only";

    private static readonly string[] CompletedStatuses =
    [
        "completed",
        "complete",
        "succeeded",
        "success"
    ];

    private static readonly string[] FailedStatuses =
    [
        "failed",
        "cancelled",
        "canceled",
        "incomplete"
    ];

    public GoogleInteractionsResult Parse(string responseJson)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(responseJson);

        using JsonDocument document = JsonDocument.Parse(responseJson);
        JsonElement root = document.RootElement;

        ValidateStatus(root);

        IReadOnlyList<GoogleInteractionImageContent> images = ExtractImages(root);

        if (images.Count == 0)
        {
            throw new GoogleInteractionsException(
                ImageGenerationProviderFailureKind.InvalidResponse,
                "The generation provider did not return a JPEG image.",
                CreateNoImageDiagnostics(root));
        }

        GenerationUsageDto usage = ExtractUsage(root);

        return new GoogleInteractionsResult(images, usage);
    }

    private static void ValidateStatus(JsonElement root)
    {
        if (!GoogleInteractionsJsonElementReader.TryGetProperty(
            root,
            StatusPropertyName,
            out JsonElement statusElement)
            && !GoogleInteractionsJsonElementReader.TryGetProperty(
                root,
                StatePropertyName,
                out statusElement))
        {
            return;
        }

        if (statusElement.ValueKind != JsonValueKind.String)
        {
            throw new GoogleInteractionsException(
                ImageGenerationProviderFailureKind.InvalidResponse,
                InvalidStatusMessage);
        }

        string? status = statusElement.GetString();

        if (string.IsNullOrWhiteSpace(status))
        {
            throw new GoogleInteractionsException(
                ImageGenerationProviderFailureKind.InvalidResponse,
                InvalidStatusMessage);
        }

        if (CompletedStatuses.Contains(status, StringComparer.OrdinalIgnoreCase))
        {
            return;
        }

        if (FailedStatuses.Contains(status, StringComparer.OrdinalIgnoreCase))
        {
            throw new GoogleInteractionsException(
                ImageGenerationProviderFailureKind.InvalidResponse,
                "The generation provider did not complete image creation.");
        }

        throw new GoogleInteractionsException(
            ImageGenerationProviderFailureKind.InvalidResponse,
            "The generation provider returned an unknown status.");
    }

    private static IReadOnlyList<GoogleInteractionImageContent> ExtractImages(JsonElement root)
    {
        List<GoogleInteractionImageContent> images = [];

        AddImagesFromProperty(
            root,
            OutputImageSnakeCasePropertyName,
            OutputImageCamelCasePropertyName,
            images);
        AddImagesFromProperty(
            root,
            OutputImagesSnakeCasePropertyName,
            OutputImagesCamelCasePropertyName,
            images);
        AddImagesFromProperty(root, OutputPropertyName, images);
        AddImagesFromSteps(root, images);

        return images;
    }

    private static void AddImagesFromSteps(
        JsonElement root,
        List<GoogleInteractionImageContent> images)
    {
        if (!GoogleInteractionsJsonElementReader.TryGetProperty(
            root,
            StepsPropertyName,
            out JsonElement stepsElement)
            || stepsElement.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        foreach (JsonElement stepElement in stepsElement.EnumerateArray())
        {
            AddImagesFromProperty(stepElement, ContentPropertyName, images);
            AddImagesFromProperty(
                stepElement,
                ModelOutputSnakeCasePropertyName,
                ModelOutputCamelCasePropertyName,
                images);
        }
    }

    private static void AddImagesFromElement(
        JsonElement element,
        List<GoogleInteractionImageContent> images)
    {
        GoogleInteractionImageContent? image = TryCreateImageContent(element);

        if (image is not null)
        {
            images.Add(image);

            return;
        }

        if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement itemElement in element.EnumerateArray())
            {
                AddImagesFromElement(itemElement, images);
            }

            return;
        }

        if (element.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        AddImagesFromProperty(element, ContentPropertyName, images);
        AddImagesFromProperty(
            element,
            ModelOutputSnakeCasePropertyName,
            ModelOutputCamelCasePropertyName,
            images);
        AddImagesFromProperty(
            element,
            OutputImageSnakeCasePropertyName,
            OutputImageCamelCasePropertyName,
            images);
        AddImagesFromProperty(
            element,
            OutputImagesSnakeCasePropertyName,
            OutputImagesCamelCasePropertyName,
            images);
        AddImagesFromProperty(element, OutputPropertyName, images);
    }

    private static void AddImagesFromProperty(
        JsonElement element,
        string propertyName,
        List<GoogleInteractionImageContent> images)
    {
        if (!GoogleInteractionsJsonElementReader.TryGetProperty(
            element,
            propertyName,
            out JsonElement propertyElement))
        {
            return;
        }

        AddImagesFromElement(propertyElement, images);
    }

    private static void AddImagesFromProperty(
        JsonElement element,
        string firstName,
        string secondName,
        List<GoogleInteractionImageContent> images)
    {
        if (!TryGetProperty(element, firstName, secondName, out JsonElement propertyElement))
        {
            return;
        }

        AddImagesFromElement(propertyElement, images);
    }

    private static GoogleInteractionImageContent? TryCreateImageContent(JsonElement contentItemElement)
    {
        if (TryGetProperty(contentItemElement, "inline_data", "inlineData", out JsonElement inlineDataElement)
            && inlineDataElement.ValueKind == JsonValueKind.Object)
        {
            return TryCreateImageContentFromFields(inlineDataElement);
        }

        return TryCreateImageContentFromFields(contentItemElement);
    }

    private static GoogleInteractionImageContent? TryCreateImageContentFromFields(JsonElement element)
    {
        if (!TryGetStringProperty(
            element,
            GoogleInteractionsContentContract.DataPropertyName,
            out string? base64Data)
            || string.IsNullOrWhiteSpace(base64Data))
        {
            return null;
        }

        if (!TryGetStringProperty(
            element,
            GoogleInteractionsContentContract.MimeTypePropertyName,
            "mimeType",
            out string? contentType)
            || string.IsNullOrWhiteSpace(contentType))
        {
            return null;
        }

        string normalizedContentType = contentType.Trim().ToLowerInvariant();

        if (!string.Equals(
                normalizedContentType,
                GoogleInteractionsImageOutputContract.ContentType,
                StringComparison.Ordinal))
        {
            return null;
        }

        if (!IsValidBase64(base64Data))
        {
            return null;
        }

        return new GoogleInteractionImageContent(normalizedContentType, base64Data);
    }

    private static bool IsValidBase64(string base64Data)
    {
        try
        {
            Convert.FromBase64String(base64Data);

            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static GoogleInteractionsNoImageDiagnostics CreateNoImageDiagnostics(JsonElement root)
    {
        TextContentDiagnostics textContentDiagnostics = AnalyzeTextContent(root);
        string category = textContentDiagnostics.TextContentItemCount > 0
            ? TextOnlyCategory
            : NoImageCategory;

        return new GoogleInteractionsNoImageDiagnostics(
            category,
            ExtractStatus(root),
            TryGetProperty(
                root,
                OutputImageSnakeCasePropertyName,
                OutputImageCamelCasePropertyName,
                out _),
            GoogleInteractionsJsonElementReader.TryGetProperty(root, OutputPropertyName, out _),
            TryGetProperty(
                root,
                OutputImagesSnakeCasePropertyName,
                OutputImagesCamelCasePropertyName,
                out _),
            textContentDiagnostics.HasStepsTextContent,
            textContentDiagnostics.HasModelOutputTextContent,
            textContentDiagnostics.HasContentTextContent,
            textContentDiagnostics.TextContentLength,
            textContentDiagnostics.TextContentItemCount);
    }

    private static TextContentDiagnostics AnalyzeTextContent(JsonElement root)
    {
        TextContentDiagnosticsBuilder builder = new();
        AnalyzeTextContentElement(root, builder, false, false, false);

        return builder.Build();
    }

    private static void AnalyzeTextContentElement(
        JsonElement element,
        TextContentDiagnosticsBuilder builder,
        bool isInsideSteps,
        bool isInsideModelOutput,
        bool isInsideContent)
    {
        if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement itemElement in element.EnumerateArray())
            {
                AnalyzeTextContentElement(
                    itemElement,
                    builder,
                    isInsideSteps,
                    isInsideModelOutput,
                    isInsideContent);
            }

            return;
        }

        if (element.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        AddTextContentDiagnostic(element, builder, isInsideSteps, isInsideModelOutput, isInsideContent);

        foreach (JsonProperty property in element.EnumerateObject())
        {
            AnalyzeTextContentElement(
                property.Value,
                builder,
                isInsideSteps || IsPropertyName(property, StepsPropertyName),
                isInsideModelOutput
                    || IsPropertyName(
                        property,
                        ModelOutputSnakeCasePropertyName,
                        ModelOutputCamelCasePropertyName),
                isInsideContent || IsPropertyName(property, ContentPropertyName));
        }
    }

    private static void AddTextContentDiagnostic(
        JsonElement element,
        TextContentDiagnosticsBuilder builder,
        bool isInsideSteps,
        bool isInsideModelOutput,
        bool isInsideContent)
    {
        if (!TryGetStringProperty(
            element,
            GoogleInteractionsContentContract.TypePropertyName,
            out string? type)
            || !string.Equals(
                type,
                GoogleInteractionsContentContract.TextType,
                StringComparison.OrdinalIgnoreCase)
            || !TryGetStringProperty(
                element,
                GoogleInteractionsContentContract.TextPropertyName,
                out string? text))
        {
            return;
        }

        builder.AddTextContent(
            text.Length,
            isInsideSteps,
            isInsideModelOutput,
            isInsideContent);
    }

    private static string? ExtractStatus(JsonElement root)
    {
        if (TryGetStringProperty(root, StatusPropertyName, out string? status))
        {
            return status;
        }

        if (TryGetStringProperty(root, StatePropertyName, out status))
        {
            return status;
        }

        return null;
    }

    private static GenerationUsageDto ExtractUsage(JsonElement root)
    {
        if (!GoogleInteractionsJsonElementReader.TryGetProperty(
            root,
            "usage",
            out JsonElement usageElement)
            || usageElement.ValueKind != JsonValueKind.Object)
        {
            throw new GoogleInteractionsException(
                ImageGenerationProviderFailureKind.InvalidResponse,
                "The generation provider did not return usage data.");
        }

        if (!TryGetInt32Property(usageElement, "total_tokens", "totalTokens", out int totalTokens))
        {
            throw new GoogleInteractionsException(
                ImageGenerationProviderFailureKind.InvalidResponse,
                IncompleteUsageMessage);
        }

        if (!TryGetInt32Property(usageElement, "total_input_tokens", "totalInputTokens", out int inputTokens)
            || !TryGetInt32Property(usageElement, "total_output_tokens", "totalOutputTokens", out int outputTokens))
        {
            throw new GoogleInteractionsException(
                ImageGenerationProviderFailureKind.InvalidResponse,
                IncompleteUsageMessage);
        }

        if (totalTokens < 0 || inputTokens < 0 || outputTokens < 0)
        {
            throw CreateInvalidUsageException();
        }

        IReadOnlyList<GenerationModalityTokensDto>? inputTokensByModality = ExtractModalityTokens(
            usageElement,
            "input_tokens_by_modality",
            "inputTokensByModality");
        IReadOnlyList<GenerationModalityTokensDto>? outputTokensByModality = ExtractModalityTokens(
            usageElement,
            "output_tokens_by_modality",
            "outputTokensByModality");
        int? thoughtTokens = ExtractOptionalNonNegativeInt32(
            usageElement,
            "total_thought_tokens",
            "totalThoughtTokens");
        int? toolUseTokens = ExtractOptionalNonNegativeInt32(
            usageElement,
            "total_tool_use_tokens",
            "totalToolUseTokens");
        int? cachedTokens = ExtractOptionalNonNegativeInt32(
            usageElement,
            "total_cached_tokens",
            "totalCachedTokens");

        return new GenerationUsageDto(
            TotalTokens: totalTokens,
            TotalInputTokens: inputTokens,
            TotalOutputTokens: outputTokens,
            InputTokensByModality: inputTokensByModality,
            OutputTokensByModality: outputTokensByModality,
            TotalThoughtTokens: thoughtTokens,
            TotalToolUseTokens: toolUseTokens,
            TotalCachedTokens: cachedTokens);
    }

    private static IReadOnlyList<GenerationModalityTokensDto>? ExtractModalityTokens(
        JsonElement usageElement,
        string firstName,
        string secondName)
    {
        if (!TryGetProperty(usageElement, firstName, secondName, out JsonElement tokensElement))
        {
            return null;
        }

        if (tokensElement.ValueKind != JsonValueKind.Array)
        {
            throw CreateInvalidUsageException();
        }

        List<GenerationModalityTokensDto> modalityTokens = [];

        foreach (JsonElement tokenElement in tokensElement.EnumerateArray())
        {
            if (tokenElement.ValueKind != JsonValueKind.Object
                || !TryGetStringProperty(tokenElement, "modality", out string? modality)
                || string.IsNullOrWhiteSpace(modality))
            {
                throw CreateInvalidUsageException();
            }

            int tokens = ExtractModalityTokenCount(tokenElement);
            string normalizedModality = GenerationUsageModalityNormalizer.Normalize(modality);

            modalityTokens.Add(new GenerationModalityTokensDto(normalizedModality, tokens));
        }

        return modalityTokens.Count == 0
            ? null
            : modalityTokens;
    }

    private static int? ExtractOptionalNonNegativeInt32(
        JsonElement element,
        string firstName,
        string secondName)
    {
        if (!TryGetProperty(element, firstName, secondName, out JsonElement propertyElement))
        {
            return null;
        }

        if (propertyElement.ValueKind != JsonValueKind.Number
            || !propertyElement.TryGetInt32(out int value)
            || value < 0)
        {
            throw CreateInvalidUsageException();
        }

        return value;
    }

    private static int ExtractModalityTokenCount(JsonElement element)
    {
        if (!GoogleInteractionsJsonElementReader.TryGetProperty(
            element,
            "tokens",
            out JsonElement tokensElement)
            && !GoogleInteractionsJsonElementReader.TryGetProperty(
                element,
                "token_count",
                out tokensElement)
            && !GoogleInteractionsJsonElementReader.TryGetProperty(
                element,
                "tokenCount",
                out tokensElement))
        {
            throw CreateInvalidUsageException();
        }

        if (tokensElement.ValueKind != JsonValueKind.Number
            || !tokensElement.TryGetInt32(out int value)
            || value < 0)
        {
            throw CreateInvalidUsageException();
        }

        return value;
    }

    private static GoogleInteractionsException CreateInvalidUsageException()
    {
        return new GoogleInteractionsException(
            ImageGenerationProviderFailureKind.InvalidResponse,
            "The generation provider returned invalid usage data.");
    }

    private static bool TryGetInt32Property(
        JsonElement element,
        string firstName,
        string secondName,
        out int value)
    {
        value = 0;

        if (!TryGetProperty(element, firstName, secondName, out JsonElement propertyElement))
        {
            return false;
        }

        return propertyElement.ValueKind == JsonValueKind.Number
            && propertyElement.TryGetInt32(out value);
    }

    private static bool TryGetStringProperty(
        JsonElement element,
        string name,
        [NotNullWhen(true)] out string? value)
    {
        value = null;

        if (!GoogleInteractionsJsonElementReader.TryGetProperty(
            element,
            name,
            out JsonElement propertyElement)
            || propertyElement.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        value = propertyElement.GetString();

        return value is not null;
    }

    private static bool TryGetStringProperty(
        JsonElement element,
        string firstName,
        string secondName,
        [NotNullWhen(true)] out string? value)
    {
        value = null;

        if (!TryGetProperty(element, firstName, secondName, out JsonElement propertyElement)
            || propertyElement.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        value = propertyElement.GetString();

        return value is not null;
    }

    private static bool TryGetProperty(
        JsonElement element,
        string firstName,
        string secondName,
        out JsonElement propertyElement)
    {
        if (GoogleInteractionsJsonElementReader.TryGetProperty(
            element,
            firstName,
            out propertyElement))
        {
            return true;
        }

        return GoogleInteractionsJsonElementReader.TryGetProperty(
            element,
            secondName,
            out propertyElement);
    }

    private static bool IsPropertyName(JsonProperty property, string name)
    {
        return string.Equals(property.Name, name, StringComparison.Ordinal);
    }

    private static bool IsPropertyName(JsonProperty property, string firstName, string secondName)
    {
        return IsPropertyName(property, firstName)
            || IsPropertyName(property, secondName);
    }

    private sealed record TextContentDiagnostics(
        bool HasStepsTextContent,
        bool HasModelOutputTextContent,
        bool HasContentTextContent,
        int TextContentLength,
        int TextContentItemCount);

    private sealed class TextContentDiagnosticsBuilder
    {
        public bool HasStepsTextContent { get; private set; }
        public bool HasModelOutputTextContent { get; private set; }
        public bool HasContentTextContent { get; private set; }
        public int TextContentLength { get; private set; }
        public int TextContentItemCount { get; private set; }

        public void AddTextContent(
            int textLength,
            bool isInsideSteps,
            bool isInsideModelOutput,
            bool isInsideContent)
        {
            TextContentLength += textLength;
            TextContentItemCount++;
            HasStepsTextContent = HasStepsTextContent || isInsideSteps;
            HasModelOutputTextContent = HasModelOutputTextContent || isInsideModelOutput;
            HasContentTextContent = HasContentTextContent || isInsideContent;
        }

        public TextContentDiagnostics Build()
        {
            return new TextContentDiagnostics(
                HasStepsTextContent,
                HasModelOutputTextContent,
                HasContentTextContent,
                TextContentLength,
                TextContentItemCount);
        }
    }
}
