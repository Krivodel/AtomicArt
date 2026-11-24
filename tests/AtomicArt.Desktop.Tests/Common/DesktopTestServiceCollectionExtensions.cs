using Microsoft.Extensions.DependencyInjection;

using AtomicArt.Desktop.Services.Paths;

namespace AtomicArt.Desktop.Tests.Common;

internal static class DesktopTestServiceCollectionExtensions
{
    internal static void AddIsolatedDesktopServices(this IServiceCollection services, string? dataRoot = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        string root = dataRoot ?? DesktopTestDirectories.CreateUniqueDirectoryPath();
        services.AddSingleton(new AtomicArtDataPathProvider(root));
        services.AddDesktopServices();
        services.AddSingleton(new AtomicArtDataRootBootstrapStore(Path.Combine(root, "Bootstrap"), root));
    }
}
