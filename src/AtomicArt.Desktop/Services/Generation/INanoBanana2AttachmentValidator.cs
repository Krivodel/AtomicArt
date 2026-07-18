using AtomicArt.Contracts.Generation;

namespace AtomicArt.Desktop.Services.Generation;

public interface INanoBanana2AttachmentValidator
{
    IReadOnlyList<AttachedImageDto>? CreateValidatedAttachments(
        ImageModelOption selectedModel,
        IReadOnlyList<AttachedImageDto> currentImages,
        IReadOnlyList<AttachedImageDto>? incomingImages);
}
