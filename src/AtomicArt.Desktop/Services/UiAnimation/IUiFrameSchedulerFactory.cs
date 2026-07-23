using Avalonia.Controls;

namespace AtomicArt.Desktop.Services.UiAnimation;

internal interface IUiFrameSchedulerFactory
{
    IUiFrameScheduler Create(TopLevel topLevel);
}
