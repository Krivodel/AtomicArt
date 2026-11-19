namespace AtomicArt.Desktop.Services.Generation.State;

public interface IPanelAttachmentFilePathResolver
{
    string? ResolveExistingFilePath(string panelId, PanelAttachmentState attachment);

    Task<string?> GetExistingFilePathAsync(
        string panelId,
        PanelAttachmentState attachment,
        CancellationToken ct);
}
