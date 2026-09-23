using Avalonia.Input.Platform;
using Avalonia.Platform.Storage;

namespace AtomicArt.Desktop.Services;

public interface IClipboardAttachmentService
{
    void Attach(IClipboard clipboard, IStorageProvider storageProvider);
}
