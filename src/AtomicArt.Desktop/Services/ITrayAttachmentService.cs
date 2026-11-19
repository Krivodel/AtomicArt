using Avalonia.Controls.ApplicationLifetimes;

namespace AtomicArt.Desktop.Services;

public interface ITrayAttachmentService
{
    void Attach(IClassicDesktopStyleApplicationLifetime desktopLifetime);
}
