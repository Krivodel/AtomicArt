using Avalonia.Controls;

using PicaUiFrameScheduler = Pica.Viewer.Services.AvaloniaUiFrameScheduler;

namespace AtomicArt.Desktop.Services.UiAnimation;

internal sealed class AvaloniaUiFrameScheduler : IUiFrameScheduler, IDisposable
{
    private readonly PicaUiFrameScheduler _inner;

    public AvaloniaUiFrameScheduler(TopLevel topLevel)
    {
        _inner = new PicaUiFrameScheduler(
            topLevel ?? throw new ArgumentNullException(nameof(topLevel)));
    }

    public void RequestAnimationFrame(Action<TimeSpan> frameAction)
    {
        _inner.RequestAnimationFrame(frameAction);
    }

    public void Dispose()
    {
        _inner.Dispose();
    }
}
