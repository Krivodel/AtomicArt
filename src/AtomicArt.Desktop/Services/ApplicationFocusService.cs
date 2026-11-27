using System.Runtime.InteropServices;

using Avalonia.Controls.ApplicationLifetimes;
using Pica.Viewer.Services;

namespace AtomicArt.Desktop.Services;

public sealed class ApplicationFocusService : IApplicationFocusService
{
    public bool IsFocused
    {
        get
        {
            if (!OperatingSystem.IsWindows())
            {
                return Avalonia.Application.Current?.ApplicationLifetime
                    is IClassicDesktopStyleApplicationLifetime desktop
                    && desktop.Windows.Any(window => window.IsActive);
            }

            nint foreground = GetForegroundWindow();

            return (foreground != 0)
                && (GetWindowThreadProcessId(foreground, out uint processId) != 0)
                && (processId == (uint)Environment.ProcessId);
        }
    }

    [DllImport(WindowsNativeLibraryNames.User32)]
    private static extern nint GetForegroundWindow();

    [DllImport(WindowsNativeLibraryNames.User32)]
    private static extern uint GetWindowThreadProcessId(nint window, out uint processId);
}
