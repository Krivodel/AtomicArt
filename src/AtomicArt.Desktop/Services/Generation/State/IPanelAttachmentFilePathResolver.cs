namespace AtomicArt.Desktop.Services.Generation.State;

public interface IPanelAttachmentFilePathResolver
{
    Task<string?> GetExistingFilePathAsync(
        string panelId,
        PanelAttachmentState attachment,
        CancellationToken ct);
}
