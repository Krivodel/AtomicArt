using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;

using Pica.Viewer.Services;

namespace AtomicArt.Desktop.Services.Windows;

internal static class WindowsNativeDragDrop
{
    private const string Ole32Library = "ole32.dll";

    public const int Succeeded = 0;

    [DllImport(
        WindowsNativeLibraryNames.User32,
        CharSet = CharSet.Unicode,
        EntryPoint = "GetPropW",
        SetLastError = true)]
    public static extern nint GetWindowProperty(
        nint windowHandle,
        string propertyName);

    [DllImport(WindowsNativeLibraryNames.User32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool IsWindow(nint windowHandle);

    [DllImport(Ole32Library)]
    public static extern int RegisterDragDrop(
        nint windowHandle,
        [MarshalAs(UnmanagedType.Interface)] IOleDropTarget dropTarget);

    [DllImport(Ole32Library)]
    public static extern int RevokeDragDrop(nint windowHandle);

    [DllImport(Ole32Library)]
    public static extern void ReleaseStgMedium(ref STGMEDIUM medium);

    [DllImport(WindowsNativeLibraryNames.Kernel32, SetLastError = true)]
    public static extern nint GlobalLock(nint memoryHandle);

    [DllImport(WindowsNativeLibraryNames.Kernel32, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GlobalUnlock(nint memoryHandle);

    [DllImport(WindowsNativeLibraryNames.Kernel32, SetLastError = true)]
    public static extern nuint GlobalSize(nint memoryHandle);

    [DllImport(
        WindowsNativeLibraryNames.User32,
        CharSet = CharSet.Unicode,
        EntryPoint = "RegisterClipboardFormatW",
        SetLastError = true)]
    public static extern uint RegisterClipboardFormat(string formatName);
}
