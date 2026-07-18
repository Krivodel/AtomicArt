using Avalonia.Platform.Storage;

namespace AtomicArt.Desktop.Services;

public interface IFilePickerAttachmentService
{
    void Attach(IStorageProvider storageProvider);
}
