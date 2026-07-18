using Avalonia.Controls;

using AtomicArt.Desktop.Services.GalleryAnimation;

namespace AtomicArt.Desktop.Controls.Gallery;

internal sealed class AvaloniaUiFrameSchedulerFactory : IUiFrameSchedulerFactory
{
    public IUiFrameScheduler Create(TopLevel topLevel)
    {
        ArgumentNullException.ThrowIfNull(topLevel);

        return new AvaloniaUiFrameScheduler(topLevel);
    }
}
