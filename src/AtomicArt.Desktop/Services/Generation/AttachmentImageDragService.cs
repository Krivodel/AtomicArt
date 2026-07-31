using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media.Imaging;

using Microsoft.Extensions.Logging;

using AtomicArt.Desktop.Controls;
using AtomicArt.Desktop.Services.Generation.State;

namespace AtomicArt.Desktop.Services.Generation;

public sealed class AttachmentImageDragService : IAttachmentImageDragService
{
    private readonly IPanelAttachmentFilePathResolver _filePathResolver;
    private readonly ILogger<AttachmentImageDragService> _logger;

    public AttachmentImageDragService(
        IPanelAttachmentFilePathResolver filePathResolver,
        ILogger<AttachmentImageDragService> logger)
    {
        ArgumentNullException.ThrowIfNull(filePathResolver);
        ArgumentNullException.ThrowIfNull(logger);

        _filePathResolver = filePathResolver;
        _logger = logger;
    }

    public async Task DragAsync(
        Control source,
        PointerPressedEventArgs e,
        string panelId,
        PanelAttachmentState attachment,
        Bitmap? previewBitmap,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(e);
        ArgumentException.ThrowIfNullOrWhiteSpace(panelId);
        ArgumentNullException.ThrowIfNull(attachment);

        try
        {
            string? path = await _filePathResolver.GetExistingFilePathAsync(
                panelId,
                attachment,
                ct);

            if (path is null)
            {
                return;
            }

            await ImageFileDragSource.StartAsync(
                source,
                e,
                path,
                previewBitmap,
                AtomicArtImageDragSourceKind.PanelAttachment);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to start drag-and-drop for a panel attachment.");
        }
    }
}
