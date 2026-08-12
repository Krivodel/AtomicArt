using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

using Avalonia;
using Velopack;

using AtomicArt.Desktop.Services;
using AtomicArt.Desktop.Services.Logging;
using AtomicArt.Desktop.Services.Paths;
using AtomicArt.Desktop.Services.Settings;
using AtomicArt.Desktop.Services.SingleInstance;

namespace AtomicArt.Desktop;

internal sealed class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        VelopackApp.Build().Run();

        IConfiguration bootstrapConfiguration = CreateBootstrapConfiguration();
        SingleInstanceOptions singleInstanceOptions =
            LoadSingleInstanceOptions(bootstrapConfiguration);
        StorageOptions storageOptions =
            LoadStorageOptions(bootstrapConfiguration);
        AtomicArtDataRootBootstrapStore bootstrapStore = new();
        DataRootMigrationJournalStore journalStore = new(bootstrapStore);
        bool shouldOfferInitialRootDirectorySelection = false;
        Exception? bootstrapLoadFailure = null;
        string rootDirectory;

        try
        {
            rootDirectory = bootstrapStore.LoadRootDirectory();
            shouldOfferInitialRootDirectorySelection =
                bootstrapStore.ShouldOfferInitialRootDirectorySelection();
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
            SingleInstanceIdentity singleInstanceIdentity =
                SingleInstanceIdentity.CreateDefault();
            using SingleInstanceCoordinator singleInstanceCoordinator = new(
                singleInstanceIdentity,
                loggerFactory.CreateLogger<SingleInstanceCoordinator>(),
                singleInstanceOptions);

            if (!singleInstanceCoordinator.TryStartOrNotifyExisting())
            {
                logger.LogInformation(
                    "Another Atomic Art process is already running; this process will exit.");
                return;
            }

            if (bootstrapLoadFailure is not null)
            {
                throw new InvalidDataException(
                    "Atomic Art data root bootstrap state could not be loaded safely.",
                    bootstrapLoadFailure);
            }

            TryRecoverDataRootMigration(
                bootstrapStore,
                journalStore,
                storageOptions,
                logger);
            IConfiguration configuration = App.CreateConfiguration();
            App.ConfigureBootstrap(
                configuration,
                pathProvider,
                loggerProvider,
                singleInstanceCoordinator);

            if (shouldOfferInitialRootDirectorySelection)
            {
                App.RequestInitialRootDirectorySelection();
            }

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

    private static SingleInstanceOptions LoadSingleInstanceOptions(
        IConfiguration configuration)
    {
        SingleInstanceOptions options = configuration
            .GetRequiredSection(SingleInstanceOptions.SectionName)
            .Get<SingleInstanceOptions>()
            ?? throw new InvalidOperationException(
                "Single-instance configuration could not be loaded.");

        if (!SingleInstanceOptions.IsValid(options))
        {
            throw new InvalidOperationException(
                "Single-instance configuration is invalid.");
        }

        return options;
    }

    private static StorageOptions LoadStorageOptions(
        IConfiguration configuration)
    {
        StorageOptions options = configuration
            .GetRequiredSection(StorageOptions.SectionName)
            .Get<StorageOptions>()
            ?? throw new InvalidOperationException(
                "Storage configuration could not be loaded.");

        if (!StorageOptions.IsValid(options))
        {
            throw new InvalidOperationException(
                "Storage configuration is invalid.");
        }

        return options;
    }

    private static void TryRecoverDataRootMigration(
        AtomicArtDataRootBootstrapStore bootstrapStore,
        DataRootMigrationJournalStore journalStore,
        StorageOptions storageOptions,
        ILogger<Program> logger)
    {
        try
        {
            DataRootMigrationRecovery.Recover(
                bootstrapStore,
                journalStore,
                storageOptions);
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
