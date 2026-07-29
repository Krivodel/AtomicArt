namespace AtomicArt.Domain.Generation;

public sealed class MetadataGenerationModelRules : IGenerationModelRules
{
    public int Priority => 0;

    public bool CanValidate(GenerationModelConstraints constraints)
    {
        ArgumentNullException.ThrowIfNull(constraints);

        return true;
    }

    public GenerationValidationResult Validate(GenerationValidationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        GenerationValidationResult? parameterResult = ValidateParameters(request);

        if (parameterResult is not null)
        {
            return parameterResult;
        }

        return ValidateAttachedImages(request);
    }

    private static GenerationValidationResult? ValidateParameters(
        GenerationValidationRequest request)
    {
        if (request.Prompt is not null
            && request.Prompt.Length > request.Constraints.MaxPromptLength)
        {
            return GenerationValidationResult.Invalid(
                GenerationErrorCodes.ModelRequestValidation,
                "The prompt exceeds the maximum length for the selected model.");
        }

        if (!request.Constraints.Resolutions.Contains(
            request.Resolution,
            StringComparer.Ordinal))
        {
            return GenerationValidationResult.Invalid(
                GenerationErrorCodes.UnsupportedResolution,
                "The selected resolution is not supported by the model.");
        }

        if (!request.Constraints.AspectRatios.Contains(
            request.AspectRatio,
            StringComparer.Ordinal))
        {
            return GenerationValidationResult.Invalid(
                GenerationErrorCodes.UnsupportedAspectRatio,
                "The selected aspect ratio is not supported by the model.");
        }

        if (!request.Constraints.Temperature.IsSupported(request.Temperature))
        {
            return GenerationValidationResult.Invalid(
                GenerationErrorCodes.ModelRequestValidation,
                "The selected temperature is not supported by the model.");
        }

        bool isThinkingLevelSupported = request.Constraints.Thinking is null
            ? string.IsNullOrWhiteSpace(request.ThinkingLevel)
            : request.Constraints.Thinking.IsSupported(request.ThinkingLevel);

        if (!isThinkingLevelSupported)
        {
            return GenerationValidationResult.Invalid(
                GenerationErrorCodes.ModelRequestValidation,
                "The selected thinking level is not supported by the model.");
        }

        return ValidateGenerationCount(request);
    }

    private static GenerationValidationResult? ValidateGenerationCount(
        GenerationValidationRequest request)
    {
        if (!request.Constraints.GenerationCounts.Contains(request.GenerationCount))
        {
            return GenerationValidationResult.Invalid(
                GenerationErrorCodes.ModelRequestValidation,
                "The selected image count is not supported by the model.");
        }

        return null;
    }

    private static GenerationValidationResult ValidateAttachedImages(
        GenerationValidationRequest request)
    {
        if (request.AttachedImages.Count > request.Constraints.MaxAttachedImages)
        {
            return GenerationValidationResult.Invalid(
                GenerationErrorCodes.ModelRequestValidation,
                "Too many images are attached to the request.");
        }

        return ValidateAttachedImageSizes(request);
    }

    private static GenerationValidationResult ValidateAttachedImageSizes(
        GenerationValidationRequest request)
    {
        long totalAttachedImageBytes = 0;

        foreach (GenerationAttachedImage attachedImage in request.AttachedImages)
        {
            GenerationValidationResult? itemResult = ValidateAttachedImage(
                request.Constraints,
                attachedImage);

            if (itemResult is not null)
            {
                return itemResult;
            }

            totalAttachedImageBytes += attachedImage.SizeInBytes;

            if (totalAttachedImageBytes > request.Constraints.MaxTotalAttachedImageBytes)
            {
                return GenerationValidationResult.Invalid(
                    GenerationErrorCodes.ModelRequestValidation,
                    "The total attachment size exceeds the allowed limit.");
            }
        }

        return GenerationValidationResult.Valid();
    }

    private static GenerationValidationResult? ValidateAttachedImage(
        GenerationModelConstraints constraints,
        GenerationAttachedImage? attachedImage)
    {
        if (attachedImage is null)
        {
            return GenerationValidationResult.Invalid(
                GenerationErrorCodes.ModelRequestValidation,
                "The attached image was not provided.");
        }

        GenerationValidationResult? sizeResult = ValidateAttachedImageSize(constraints, attachedImage.SizeInBytes);

        if (sizeResult is not null)
        {
            return sizeResult;
        }

        if (!IsSupportedContentType(constraints, attachedImage.ContentType))
        {
            return GenerationValidationResult.Invalid(
                GenerationErrorCodes.ModelRequestValidation,
                "The attachment content type is not supported by the selected model.");
        }

        return null;
    }

    private static GenerationValidationResult? ValidateAttachedImageSize(
        GenerationModelConstraints constraints,
        long attachedImageSize)
    {
        return attachedImageSize switch
        {
            <= 0 => GenerationValidationResult.Invalid(
                GenerationErrorCodes.ModelRequestValidation,
                GenerationValidationMessages.MissingAttachedImageContent),
            long size when size > constraints.MaxAttachedImageBytes =>
                GenerationValidationResult.Invalid(
                    GenerationErrorCodes.ModelRequestValidation,
                    "The attachment size exceeds the allowed limit."),
            _ => null
        };
    }

    private static bool IsSupportedContentType(
        GenerationModelConstraints constraints,
        string? contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType))
        {
            return false;
        }

        string normalizedContentType = contentType.Trim();

        return constraints.SupportedContentTypes.Contains(normalizedContentType, StringComparer.OrdinalIgnoreCase);
    }
}
