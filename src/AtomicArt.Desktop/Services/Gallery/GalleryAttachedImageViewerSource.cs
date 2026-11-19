using AtomicArt.Desktop.Services.Generation.State;

namespace AtomicArt.Desktop.Services.Gallery;

public sealed record GalleryAttachedImageViewerSource(
    string PanelId,
    PanelAttachmentState Attachment) : GalleryImageViewerSource;
