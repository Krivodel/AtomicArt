using AtomicArt.Contracts.Generation;
using AtomicArt.Desktop.Services.Generation;

namespace AtomicArt.Desktop.Services.Gallery;

public sealed record GalleryCompletedItemUpdate(
    GenerationItemDto Item,
    string? TrustedImagePath,
    string? ThumbnailPath,
    GenerationChatResult? ChatResult = null);
