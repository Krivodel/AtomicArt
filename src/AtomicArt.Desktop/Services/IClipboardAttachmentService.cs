using Avalonia.Input.Platform;

namespace AtomicArt.Desktop.Services;

public interface IClipboardAttachmentService
{
    void Attach(IClipboard clipboard);
}
