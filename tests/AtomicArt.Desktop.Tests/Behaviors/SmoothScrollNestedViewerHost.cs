using Avalonia.Controls;

namespace AtomicArt.Desktop.Tests.Behaviors;

internal sealed class SmoothScrollNestedViewerHost : IDisposable
{
    internal required Window Window { get; init; }
    internal required ScrollViewer OuterViewer { get; init; }
    internal required ScrollViewer InnerViewer { get; init; }

    public void Dispose()
    {
        Window.Close();
    }
}
