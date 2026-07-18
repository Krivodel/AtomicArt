using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

using Avalonia;
using Velopack;

using AtomicArt.Desktop.Services.Logging;
using AtomicArt.Desktop.Services.Paths;
using AtomicArt.Desktop.Services.Settings;

namespace AtomicArt.Desktop;

internal sealed class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        VelopackApp.Build().Run();

        IConfiguration bootstrapConfiguration = CreateBootstrapConfiguration();
        AtomicArtDataPathProvider pathProvider = new();
        DesktopFileLoggingOptions loggingOptions = new(bootstrapConfiguration);
        DesktopFileLoggerProvider loggerProvider = new(pathProvider, loggingOptions);
        using ILoggerFactory loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Debug);
            builder.AddProvider(loggerProvider);
        });
        ILogger<Program> logger = loggerFactory.CreateLogger<Program>();

        try
        {
            IConfiguration configuration = App.CreateConfiguration();
            App.ConfigureBootstrap(configuration, loggerProvider);
            logger.LogInformation("Atomic Art desktop process is starting.");

            long maxGpuResourceSizeBytes =
                GpuResourceCacheStartupSettingsReader.LoadMaxGpuResourceSizeBytes();
            logger.LogInformation(
                "Early GPU resource cache setting resolved to {MaxGpuResourceSizeBytes} bytes.",
                maxGpuResourceSizeBytes);

            BuildAvaloniaApp(maxGpuResourceSizeBytes)
                .StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex, "Atomic Art desktop process failed during startup or lifetime.");
            throw;
        }
        finally
        {
            App.ClearBootstrap();
        }
    }

    public static AppBuilder BuildAvaloniaApp()
    {
        return BuildAvaloniaApp(
            GpuResourceCacheStartupSettingsReader.LoadMaxGpuResourceSizeBytes());
    }

    private static AppBuilder BuildAvaloniaApp(long maxGpuResourceSizeBytes)
    {
        return AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .With(new SkiaOptions
            {
                MaxGpuResourceSizeBytes = maxGpuResourceSizeBytes
            });
    }

    private static IConfiguration CreateBootstrapConfiguration()
    {
        return new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile(DesktopConfigurationFile.Name, optional: true)
            .Build();
    }
}
