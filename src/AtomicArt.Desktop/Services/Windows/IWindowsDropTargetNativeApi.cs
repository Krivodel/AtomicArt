namespace AtomicArt.Desktop.Services.Windows;

internal interface IWindowsDropTargetNativeApi
{
    nint GetWindowProperty(nint windowHandle, string propertyName);

    bool IsWindow(nint windowHandle);

    int RegisterDragDrop(
        nint windowHandle,
        IOleDropTarget dropTarget);

    int RevokeDragDrop(nint windowHandle);
}
