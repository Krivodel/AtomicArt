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
        AtomicArtDataRootBootstrapStore bootstrapStore = new();
        DataRootMigrationJournalStore journalStore = new(bootstrapStore);
        Exception? bootstrapLoadFailure = null;
        string rootDirectory;

        try
        {
            rootDirectory = bootstrapStore.LoadRootDirectory();
        }
        catch (Exception ex) when (ex is IOException
            or UnauthorizedAccessException
            or NotSupportedException
            or System.Text.Json.JsonException
            or ArgumentException)
        {
            bootstrapLoadFailure = ex;
            rootDirectory = AtomicArtDataRootBootstrapStore.GetDefaultRootDirectory();
        }

        AtomicArtDataPathProvider pathProvider = new(rootDirectory);
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
            if (bootstrapLoadFailure is not null)
            {
                throw new InvalidDataException(
                    "Atomic Art data root bootstrap state could not be loaded safely.",
                    bootstrapLoadFailure);
            }

            TryRecoverDataRootMigration(bootstrapStore, journalStore, logger);
            IConfiguration configuration = App.CreateConfiguration();
            App.ConfigureBootstrap(configuration, pathProvider, loggerProvider);
            logger.LogInformation("Atomic Art desktop process is starting.");

            long maxGpuResourceSizeBytes =
                GpuResourceCacheStartupSettingsReader.LoadMaxGpuResourceSizeBytes(pathProvider);
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
        IConfiguration configuration = CreateBootstrapConfiguration();
        AtomicArtDataRootBootstrapStore bootstrapStore = new();
        AtomicArtDataPathProvider pathProvider = new(bootstrapStore.LoadRootDirectory());
        long maxGpuResourceSizeBytes =
            GpuResourceCacheStartupSettingsReader.LoadMaxGpuResourceSizeBytes(pathProvider);

        return BuildAvaloniaApp(maxGpuResourceSizeBytes);
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

    private static void TryRecoverDataRootMigration(
        AtomicArtDataRootBootstrapStore bootstrapStore,
        DataRootMigrationJournalStore journalStore,
        ILogger<Program> logger)
    {
        try
        {
            DataRootMigrationRecovery.Recover(bootstrapStore, journalStore);
        }
        catch (Exception ex) when (ex is IOException
            or UnauthorizedAccessException
            or NotSupportedException
            or System.Text.Json.JsonException
            or ArgumentException)
        {
            logger.LogWarning(
                ex,
                "An interrupted Atomic Art data root migration could not be fully recovered during startup.");
        }
    }
}
