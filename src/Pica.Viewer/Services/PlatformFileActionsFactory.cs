using Microsoft.Extensions.Logging;

namespace Pica.Viewer.Services;

internal static class PlatformFileActionsFactory
{
    public static IPlatformFileActions Create(
        ILogger<WindowsApplicationIconLoader> applicationIconLogger)
    {
        ArgumentNullException.ThrowIfNull(applicationIconLogger);

        return OperatingSystem.IsWindows()
            ? new WindowsFileActions(new WindowsApplicationIconLoader(applicationIconLogger))
            : new CrossPlatformFileActions();
    }
}
