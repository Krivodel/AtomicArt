using Avalonia.Controls;

namespace AtomicArt.Desktop.Tests.Behaviors;

internal sealed class SmoothScrollViewerHost : IDisposable
{
    internal required Window Window { get; init; }
    internal required Border Parent { get; init; }
    internal required ScrollViewer Viewer { get; init; }

    public void Dispose()
    {
        Window.Close();
    }
}
