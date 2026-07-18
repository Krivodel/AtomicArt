using System.Runtime.InteropServices;

namespace Pica.Viewer.Services;

internal static class WindowsShellNativeMethods
{
    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = true)]
    internal static extern int SHAssocEnumHandlers(
        string extension,
        WindowsAssocFilter filter,
        [MarshalAs(UnmanagedType.Interface)] out IWindowsEnumAssocHandlers handlers);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = true)]
    internal static extern int SHOpenWithDialog(
        nint parentWindow,
        WindowsOpenAsInfo openAsInfo);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = true)]
    internal static extern int SHCreateItemFromParsingName(
        string path,
        nint bindContext,
        ref Guid interfaceId,
        [MarshalAs(UnmanagedType.Interface)] out IWindowsShellItem shellItem);

    [DllImport("shell32.dll", EntryPoint = "ExtractIconExW", CharSet = CharSet.Unicode)]
    internal static extern int ExtractIconEx(
        string filePath,
        int iconIndex,
        out nint largeIcon,
        out nint smallIcon,
        int iconCount);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool DestroyIcon(nint icon);
}
