using AtomicArt.Contracts.Generation;

namespace AtomicArt.Desktop.Services.Generation.State;

public interface IPanelAttachmentStore
{
    PanelAttachmentState CreateState(AttachedImageDto image);

    Task SaveAsync(
        string panelId,
        PanelAttachmentState attachment,
        AttachedImageDto image,
        CancellationToken ct);

    Task<AttachedImageDto?> LoadAsync(
        string panelId,
        PanelAttachmentState attachment,
        CancellationToken ct);

    Task DeleteAsync(
        string panelId,
        PanelAttachmentState attachment,
        CancellationToken ct);
}
