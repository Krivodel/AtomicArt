using Avalonia.Controls;

namespace AtomicArt.Desktop.Services.UiAnimation;

internal sealed class AvaloniaUiFrameSchedulerFactory : IUiFrameSchedulerFactory
{
    public IUiFrameScheduler Create(TopLevel topLevel)
    {
        ArgumentNullException.ThrowIfNull(topLevel);

        return new AvaloniaUiFrameScheduler(topLevel);
    }
}
