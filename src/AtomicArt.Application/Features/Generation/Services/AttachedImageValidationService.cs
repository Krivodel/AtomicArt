using AtomicArt.Application.Common.Models;
using AtomicArt.Application.Features.Generation.Models;
using AtomicArt.Contracts.Generation;
using AtomicArt.Domain.Generation;

namespace AtomicArt.Application.Features.Generation.Services;

internal static class AttachedImageValidationService
{
    internal static Result<IReadOnlyList<AttachedImageDto>> Validate(
        IReadOnlyList<AttachedImageDto>? attachedImages,
        AttachedImageValidationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (attachedImages is null)
        {
            return Result<IReadOnlyList<AttachedImageDto>>.ValidationError(
                GenerationErrorCodes.ModelRequestValidation,
                "Список вложений не передан.");
        }

        return ValidateItems(attachedImages, options);
    }

    private static Result<IReadOnlyList<AttachedImageDto>> ValidateItems(
        IReadOnlyList<AttachedImageDto> attachedImages,
        AttachedImageValidationOptions options)
    {
        List<AttachedImageDto> normalizedAttachedImages = [];

        foreach (AttachedImageDto? attachedImage in attachedImages)
        {
            Result<AttachedImageDto> result = ValidateItem(attachedImage, options);

            if (result is not { IsSuccess: true, Value: { } normalizedAttachedImage })
            {
                return CreateAttachedImagesError(result);
            }

            normalizedAttachedImages.Add(normalizedAttachedImage);
        }

        return Result<IReadOnlyList<AttachedImageDto>>.Success(normalizedAttachedImages);
    }

    private static Result<IReadOnlyList<AttachedImageDto>> CreateAttachedImagesError(
        Result<AttachedImageDto> result)
    {
        return Result<IReadOnlyList<AttachedImageDto>>.ValidationError(
            result.ErrorCode ?? GenerationErrorCodes.ModelRequestValidation,
            result.ErrorMessage ?? "Вложение не прошло проверку.");
    }

    private static Result<AttachedImageDto> ValidateItem(
        AttachedImageDto? attachedImage,
        AttachedImageValidationOptions options)
    {
        if (attachedImage is null)
        {
            return Result<AttachedImageDto>.ValidationError(
                GenerationErrorCodes.ModelRequestValidation,
                "К запросу прикреплено некорректное изображение.");
        }

        Result<AttachedImageDto>? contentResult = ValidateContent(attachedImage);

        if (contentResult is not null)
        {
            return contentResult;
        }

        return ValidateMetadata(attachedImage, options);
    }

    private static Result<AttachedImageDto>? ValidateContent(
        AttachedImageDto attachedImage)
    {
        if (!AttachedImageValidationPolicy.HasContent(attachedImage.Content))
        {
            return Result<AttachedImageDto>.ValidationError(
                GenerationErrorCodes.ModelRequestValidation,
                GenerationValidationMessages.MissingAttachedImageContent);
        }

        return null;
    }

    private static Result<AttachedImageDto> ValidateMetadata(
        AttachedImageDto attachedImage,
        AttachedImageValidationOptions options)
    {
        string? fileName = AttachedImageValidationPolicy.NormalizeFileName(attachedImage.FileName);
        AttachedImageSignatureRule? signatureRule = AttachedImageValidationPolicy.GetSignatureRule(
            attachedImage.ContentType,
            options.SignatureRules);

        if (fileName is null || signatureRule is null)
        {
            return Result<AttachedImageDto>.ValidationError(
                GenerationErrorCodes.ModelRequestValidation,
                "Метаданные вложения не прошли проверку.");
        }

        if (!AttachedImageValidationPolicy.HasValidSignature(
                attachedImage.ContentType,
                attachedImage.Content,
                options.SignatureRules))
        {
            return Result<AttachedImageDto>.ValidationError(
                GenerationErrorCodes.ModelRequestValidation,
                "Сигнатура изображения не соответствует типу содержимого.");
        }

        return Result<AttachedImageDto>.Success(attachedImage with
        {
            FileName = fileName,
            ContentType = signatureRule.ContentType
        });
    }

}
