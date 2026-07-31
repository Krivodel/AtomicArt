using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media.Imaging;

using AtomicArt.Desktop.Services.Generation.State;

namespace AtomicArt.Desktop.Services.Generation;

public interface IAttachmentImageDragService
{
    Task DragAsync(
        Control source,
        PointerPressedEventArgs e,
        string panelId,
        PanelAttachmentState attachment,
        Bitmap? previewBitmap,
        CancellationToken ct);
}
