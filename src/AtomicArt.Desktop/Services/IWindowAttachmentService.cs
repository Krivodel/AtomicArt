using Avalonia.Controls;

namespace AtomicArt.Desktop.Services;

public interface IWindowAttachmentService
{
    void Attach(Window window);
}
