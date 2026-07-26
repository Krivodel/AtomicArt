using System.Runtime.InteropServices;

using Pica.Viewer.Services;

namespace AtomicArt.Desktop.Services.SingleInstance;

internal static class WindowsForegroundPermission
{
    public static bool TryGrantToProcess(int processId)
    {
        if (!OperatingSystem.IsWindows() || processId <= 0)
        {
            return true;
        }

        return AllowSetForegroundWindow(checked((uint)processId));
    }

    [DllImport(WindowsNativeLibraryNames.User32, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AllowSetForegroundWindow(uint processId);
}
