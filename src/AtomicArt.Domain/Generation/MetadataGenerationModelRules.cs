namespace AtomicArt.Domain.Generation;

public sealed class MetadataGenerationModelRules : IGenerationModelRules
{
    public int Priority => 0;

    public bool CanValidate(GenerationModelConstraints constraints)
    {
        ArgumentNullException.ThrowIfNull(constraints);

        return true;
    }

    public GenerationValidationResult Validate(
        GenerationModelConstraints constraints,
        string? prompt,
        string aspectRatio,
        string resolution,
        double temperature,
        int generationCount,
        IReadOnlyList<GenerationAttachedImage> attachedImages,
        string? thinkingLevel = null)
    {
        ArgumentNullException.ThrowIfNull(constraints);
        ArgumentNullException.ThrowIfNull(aspectRatio);
        ArgumentNullException.ThrowIfNull(resolution);
        ArgumentNullException.ThrowIfNull(attachedImages);

        GenerationValidationResult? parameterResult = ValidateParameters(
            constraints,
            prompt,
            aspectRatio,
            resolution,
            temperature,
            generationCount,
            thinkingLevel);

        if (parameterResult is not null)
        {
            return parameterResult;
        }

        return ValidateAttachedImages(constraints, attachedImages);
    }

    private static GenerationValidationResult? ValidateParameters(
        GenerationModelConstraints constraints,
        string? prompt,
        string aspectRatio,
        string resolution,
        double temperature,
        int generationCount,
        string? thinkingLevel)
    {
        if (prompt is not null && prompt.Length > constraints.MaxPromptLength)
        {
            return GenerationValidationResult.Invalid(
                GenerationErrorCodes.ModelRequestValidation,
                "Промпт превышает допустимую длину для выбранной модели.");
        }

        if (!constraints.Resolutions.Contains(resolution, StringComparer.Ordinal))
        {
            return GenerationValidationResult.Invalid(
                GenerationErrorCodes.UnsupportedResolution,
                "Выбранное разрешение не поддерживается моделью.");
        }

        if (!constraints.AspectRatios.Contains(aspectRatio, StringComparer.Ordinal))
        {
            return GenerationValidationResult.Invalid(
                GenerationErrorCodes.UnsupportedAspectRatio,
                "Выбранное соотношение сторон не поддерживается моделью.");
        }

        if (!constraints.Temperature.IsSupported(temperature))
        {
            return GenerationValidationResult.Invalid(
                GenerationErrorCodes.ModelRequestValidation,
                "Выбранная температура не поддерживается моделью.");
        }

        if (constraints.Thinking is null)
        {
            if (!string.IsNullOrWhiteSpace(thinkingLevel))
            {
                return GenerationValidationResult.Invalid(
                    GenerationErrorCodes.ModelRequestValidation,
                    "Выбранный уровень рассуждения не поддерживается моделью.");
            }
        }
        else if (!constraints.Thinking.IsSupported(thinkingLevel))
        {
            return GenerationValidationResult.Invalid(
                GenerationErrorCodes.ModelRequestValidation,
                "Выбранный уровень рассуждения не поддерживается моделью.");
        }

        return ValidateGenerationCount(constraints, generationCount);
    }

    private static GenerationValidationResult? ValidateGenerationCount(
        GenerationModelConstraints constraints,
        int generationCount)
    {
        if (!constraints.GenerationCounts.Contains(generationCount))
        {
            return GenerationValidationResult.Invalid(
                GenerationErrorCodes.ModelRequestValidation,
                "Выбранное количество изображений не поддерживается моделью.");
        }

        return null;
    }

    private static GenerationValidationResult ValidateAttachedImages(
        GenerationModelConstraints constraints,
        IReadOnlyList<GenerationAttachedImage> attachedImages)
    {
        if (attachedImages.Count > constraints.MaxAttachedImages)
        {
            return GenerationValidationResult.Invalid(
                GenerationErrorCodes.ModelRequestValidation,
                "К запросу прикреплено слишком много изображений.");
        }

        return ValidateAttachedImageSizes(constraints, attachedImages);
    }

    private static GenerationValidationResult ValidateAttachedImageSizes(
        GenerationModelConstraints constraints,
        IReadOnlyList<GenerationAttachedImage> attachedImages)
    {
        long totalAttachedImageBytes = 0;

        foreach (GenerationAttachedImage attachedImage in attachedImages)
        {
            GenerationValidationResult? itemResult = ValidateAttachedImage(constraints, attachedImage);

            if (itemResult is not null)
            {
                return itemResult;
            }

            totalAttachedImageBytes += attachedImage.SizeInBytes;

            if (totalAttachedImageBytes > constraints.MaxTotalAttachedImageBytes)
            {
                return GenerationValidationResult.Invalid(
                    GenerationErrorCodes.ModelRequestValidation,
                    "Суммарный размер вложений превышает допустимый лимит.");
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
            return GenerationValidationResult.Invalid(GenerationErrorCodes.ModelRequestValidation, "Вложение не передано.");
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
                "Тип содержимого вложения не поддерживается выбранной моделью.");
        }

        return null;
    }

    private static GenerationValidationResult? ValidateAttachedImageSize(
        GenerationModelConstraints constraints,
        long attachedImageSize)
    {
        return attachedImageSize switch
        {
            <= 0 => GenerationValidationResult.Invalid(GenerationErrorCodes.ModelRequestValidation, "Содержимое вложения не передано."),
            var size when size > constraints.MaxAttachedImageBytes => GenerationValidationResult.Invalid(GenerationErrorCodes.ModelRequestValidation, "Размер вложения превышает допустимый лимит."),
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
