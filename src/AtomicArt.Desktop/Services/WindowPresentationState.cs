using Avalonia.Controls;

namespace AtomicArt.Desktop.Services;

internal static class WindowPresentationState
{
    public static bool IsPresented(TopLevel? topLevel)
    {
        return topLevel is { IsVisible: true }
            && (topLevel is not Window window
                || window.WindowState != WindowState.Minimized);
    }
}
