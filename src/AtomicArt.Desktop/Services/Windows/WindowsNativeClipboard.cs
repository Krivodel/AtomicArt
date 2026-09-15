using System.Runtime.InteropServices;
using System.Text;

using Pica.Viewer.Services;

namespace AtomicArt.Desktop.Services.Windows;

internal static class WindowsNativeClipboard
{
    private const string Shell32Library = "shell32.dll";

    [DllImport(WindowsNativeLibraryNames.User32, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool OpenClipboard(nint windowHandle);

    [DllImport(WindowsNativeLibraryNames.User32, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool CloseClipboard();

    [DllImport(WindowsNativeLibraryNames.User32, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool IsClipboardFormatAvailable(uint formatId);

    [DllImport(WindowsNativeLibraryNames.User32, SetLastError = true)]
    public static extern nint GetClipboardData(uint formatId);

    [DllImport(
        Shell32Library,
        CharSet = CharSet.Unicode,
        EntryPoint = "DragQueryFileW")]
    public static extern uint DragQueryFile(
        nint dropHandle,
        uint fileIndex,
        StringBuilder? filePath,
        uint characterCount);
}
