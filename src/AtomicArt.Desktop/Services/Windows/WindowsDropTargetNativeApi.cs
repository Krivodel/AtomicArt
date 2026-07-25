using System.Runtime.Versioning;

namespace AtomicArt.Desktop.Services.Windows;

[SupportedOSPlatform("windows")]
internal sealed class WindowsDropTargetNativeApi : IWindowsDropTargetNativeApi
{
    public static WindowsDropTargetNativeApi Instance { get; } =
        new WindowsDropTargetNativeApi();

    private WindowsDropTargetNativeApi()
    {
    }

    public nint GetWindowProperty(
        nint windowHandle,
        string propertyName)
    {
        return WindowsNativeDragDrop.GetWindowProperty(
            windowHandle,
            propertyName);
    }

    public bool IsWindow(nint windowHandle)
    {
        return WindowsNativeDragDrop.IsWindow(windowHandle);
    }

    public int RegisterDragDrop(
        nint windowHandle,
        IOleDropTarget dropTarget)
    {
        return WindowsNativeDragDrop.RegisterDragDrop(
            windowHandle,
            dropTarget);
    }

    public int RevokeDragDrop(nint windowHandle)
    {
        return WindowsNativeDragDrop.RevokeDragDrop(windowHandle);
    }
}
