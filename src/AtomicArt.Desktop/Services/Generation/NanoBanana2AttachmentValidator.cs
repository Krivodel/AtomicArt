using AtomicArt.Contracts.Generation;

namespace AtomicArt.Desktop.Services.Generation;

public sealed class NanoBanana2AttachmentValidator : INanoBanana2AttachmentValidator, IGenerationModelService
{
    private readonly IAttachedImageSignatureValidator _signatureValidator;

    public NanoBanana2AttachmentValidator(IAttachedImageSignatureValidator signatureValidator)
    {
        ArgumentNullException.ThrowIfNull(signatureValidator);

        _signatureValidator = signatureValidator;
    }

    public IReadOnlyList<AttachedImageDto>? CreateValidatedAttachments(
        ImageModelOption selectedModel,
        IReadOnlyList<AttachedImageDto> currentImages,
        IReadOnlyList<AttachedImageDto>? incomingImages)
    {
        ArgumentNullException.ThrowIfNull(selectedModel);
        ArgumentNullException.ThrowIfNull(currentImages);

        if (incomingImages is null || incomingImages.Count == 0)
        {
            return currentImages;
        }

        List<AttachedImageDto> candidateImages = currentImages
            .Concat(incomingImages)
            .ToList();

        // Предварительная клиентская подсказка по metadata каталога; серверные инварианты проверяются в Domain/Application.
        if (!HasValidClientPreviewResources(selectedModel, candidateImages))
        {
            return null;
        }

        if (candidateImages.Any(image => !HasValidClientPreviewContent(selectedModel, image)))
        {
            return null;
        }

        return candidateImages;
    }

    private static bool HasValidClientPreviewResources(
        ImageModelOption selectedModel,
        IReadOnlyList<AttachedImageDto> images)
    {
        if (images.Count > selectedModel.MaxAttachedImages)
        {
            return false;
        }

        long totalImageBytes = 0L;

        foreach (AttachedImageDto image in images)
        {
            if (image?.Content is null)
            {
                return false;
            }

            totalImageBytes += image.Content.LongLength;

            if (totalImageBytes > selectedModel.MaxTotalAttachedImageBytes)
            {
                return false;
            }
        }

        return true;
    }

    private bool HasValidClientPreviewContent(ImageModelOption selectedModel, AttachedImageDto? image)
    {
        return image?.Content is not null
            && selectedModel.SupportedAttachmentContentTypes.Contains(
                image.ContentType,
                StringComparer.OrdinalIgnoreCase)
            && image.Content.Length > 0
            && image.Content.Length <= selectedModel.MaxAttachedImageBytes
            && _signatureValidator.MatchesSignature(image.ContentType, image.Content);
    }
}
