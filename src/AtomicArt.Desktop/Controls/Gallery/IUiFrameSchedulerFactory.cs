using Avalonia.Controls;

using AtomicArt.Desktop.Services.GalleryAnimation;

namespace AtomicArt.Desktop.Controls.Gallery;

internal interface IUiFrameSchedulerFactory
{
    IUiFrameScheduler Create(TopLevel topLevel);
}
