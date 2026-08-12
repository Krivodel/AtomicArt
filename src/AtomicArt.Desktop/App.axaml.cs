using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;

using AtomicArt.Desktop.Services;
using AtomicArt.Desktop.Services.Logging;
using AtomicArt.Desktop.Services.Paths;
using AtomicArt.Desktop.Services.SingleInstance;
using AtomicArt.Desktop.Services.State;
using AtomicArt.Desktop.Services.Updates;
using AtomicArt.Desktop.ViewModels;
using AtomicArt.Desktop.ViewModels.Settings;
using AtomicArt.Desktop.Views;
using AtomicArt.Desktop.Views.Shell;

namespace AtomicArt.Desktop;

public class App : Avalonia.Application
{
    private static IConfiguration? s_bootstrapConfiguration;
    private static AtomicArtDataPathProvider? s_bootstrapPathProvider;
    private static DesktopFileLoggerProvider? s_bootstrapLoggerProvider;
    private static SingleInstanceCoordinator? s_singleInstanceCoordinator;
    private static bool ShouldOfferInitialRootDirectorySelection;

    private ServiceProvider? _serviceProvider;
    private ILogger<App>? _logger;
    private bool _isShutdownFlushCompleted;
    private bool _isShutdownFlushRunning;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        ServiceCollection services = new();
        IConfiguration configuration = s_bootstrapConfiguration ?? CreateConfiguration();
        services.AddSingleton(configuration);

        if (s_bootstrapLoggerProvider is null)
        {
            services.AddDesktopServices();
        }
        else
        {
            AtomicArtDataPathProvider pathProvider = s_bootstrapPathProvider
                ?? throw new InvalidOperationException(
                    "The bootstrap data path provider is unavailable.");
            services.AddDesktopServices(pathProvider, s_bootstrapLoggerProvider);
        }

        _serviceProvider = services.BuildServiceProvider();
        _logger = _serviceProvider.GetRequiredService<ILogger<App>>();
        _logger.LogInformation("Atomic Art desktop services were initialized.");

        ConfigureViewTemplates();
        ConfigureGlobalExceptionHandling();
        ConfigureDesktopLifetime();

        base.OnFrameworkInitializationCompleted();
        _logger.LogInformation("Atomic Art desktop framework initialization completed.");
    }

    internal static void ConfigureBootstrap(
        IConfiguration configuration,
        AtomicArtDataPathProvider pathProvider,
        DesktopFileLoggerProvider loggerProvider,
        SingleInstanceCoordinator singleInstanceCoordinator)
    {
        s_bootstrapConfiguration = configuration
            ?? throw new ArgumentNullException(nameof(configuration));
        s_bootstrapPathProvider = pathProvider
            ?? throw new ArgumentNullException(nameof(pathProvider));
        s_bootstrapLoggerProvider = loggerProvider
            ?? throw new ArgumentNullException(nameof(loggerProvider));
        s_singleInstanceCoordinator = singleInstanceCoordinator
            ?? throw new ArgumentNullException(
                nameof(singleInstanceCoordinator));
    }

    internal static void ClearBootstrap()
    {
        s_bootstrapConfiguration = null;
        s_bootstrapPathProvider = null;
        s_bootstrapLoggerProvider = null;
        s_singleInstanceCoordinator = null;
        ShouldOfferInitialRootDirectorySelection = false;
    }

    internal static void RequestInitialRootDirectorySelection()
    {
        ShouldOfferInitialRootDirectorySelection = true;
    }

    internal static IConfiguration CreateConfiguration()
    {
        return new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile(DesktopConfigurationFile.Name, optional: false)
            .Build();
    }

    private void StartMainWindowInitialization(MainWindow mainWindow)
    {
        if (mainWindow.DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        mainWindow.Opened += OnMainWindowOpened;

        async void OnMainWindowOpened(object? sender, EventArgs e)
        {
            mainWindow.Opened -= OnMainWindowOpened;
            await Dispatcher.UIThread.InvokeAsync(
                () => { },
                DispatcherPriority.Loaded);
            await viewModel.RestoreAppStateCommand.ExecuteAsync(null);

            if (ShouldOfferInitialRootDirectorySelection)
            {
                ShouldOfferInitialRootDirectorySelection = false;
                await OfferInitialRootDirectorySelectionAsync(viewModel);
            }

            await viewModel.ApplicationUpdate.StartMonitoringCommand.ExecuteAsync(null);
        }
    }

    private async Task OfferInitialRootDirectorySelectionAsync(
        MainWindowViewModel viewModel)
    {
        DataRootSettingViewModel? dataRootSetting = viewModel.Settings.Settings
            .OfType<DataRootSettingViewModel>()
            .SingleOrDefault();

        if (dataRootSetting is null)
        {
            _logger?.LogError(
                "The data root setting is unavailable during initial application setup.");
            return;
        }

        AtomicArtDataRootBootstrapStore bootstrapStore =
            GetRequiredService<AtomicArtDataRootBootstrapStore>();
        IAtomicArtDataPathProvider pathProvider =
            GetRequiredService<IAtomicArtDataPathProvider>();

        await PersistInitialRootDirectorySelectionStateAsync(
            () => bootstrapStore.MarkInitialRootDirectorySelectionPendingAsync(
                pathProvider.RootDirectory,
                CancellationToken.None),
            "marking as pending");

        await dataRootSetting.ChangeDirectoryCommand.ExecuteAsync(null);

        if (dataRootSetting.HasErrorMessage)
        {
            return;
        }

        await PersistInitialRootDirectorySelectionStateAsync(
            () => bootstrapStore.MarkInitialRootDirectorySelectionCompletedAsync(
                pathProvider.RootDirectory,
                CancellationToken.None),
            "marking as completed");
    }

    private async Task PersistInitialRootDirectorySelectionStateAsync(
        Func<Task> persistState,
        string operationName)
    {
        ArgumentNullException.ThrowIfNull(persistState);
        ArgumentException.ThrowIfNullOrWhiteSpace(operationName);

        try
        {
            await persistState();
        }
        catch (Exception ex) when (ex is IOException
            or UnauthorizedAccessException
            or NotSupportedException
            or ArgumentException)
        {
            _logger?.LogWarning(
                ex,
                "Initial data root selection state could not be persisted during {OperationName}.",
                operationName);
        }
    }

    private void ConfigureGlobalExceptionHandling()
    {
        GlobalExceptionService globalExceptionService = GetRequiredService<GlobalExceptionService>();
        globalExceptionService.Initialize();
    }

    private void ConfigureViewTemplates()
    {
        List<ViewTemplateRegistration> registrations = GetRequiredService<
                IEnumerable<ViewTemplateRegistration>>()
            .ToList();

        DataTemplates.Add(new ViewModelViewTemplate(registrations));
    }

    private void ConfigureDesktopLifetime()
    {
        if (ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktopLifetime)
        {
            _logger?.LogWarning("Classic desktop application lifetime is unavailable.");
            return;
        }

        MainWindow mainWindow = GetRequiredService<MainWindow>();

        IWindowStateService windowStateService =
            GetRequiredService<IWindowStateService>();
        IWindowAttachmentService windowAttachmentService = GetRequiredService<IWindowAttachmentService>();
        ITrayAttachmentService trayAttachmentService = GetRequiredService<ITrayAttachmentService>();
        IFilePickerAttachmentService filePickerAttachmentService = GetRequiredService<IFilePickerAttachmentService>();
        IClipboardAttachmentService clipboardAttachmentService = GetRequiredService<IClipboardAttachmentService>();
        IVirtualFileDropAttachmentService virtualFileDropAttachmentService =
            GetRequiredService<IVirtualFileDropAttachmentService>();

        windowAttachmentService.Attach(mainWindow);
        trayAttachmentService.Attach(mainWindow);
        filePickerAttachmentService.Attach(mainWindow.StorageProvider);
        virtualFileDropAttachmentService.Attach(mainWindow);
        s_singleInstanceCoordinator?.AttachActivationHandler(
            () => ActivateMainWindowAsync(windowStateService));

        if (mainWindow.Clipboard is not null)
        {
            clipboardAttachmentService.Attach(mainWindow.Clipboard);
        }

        desktopLifetime.MainWindow = mainWindow;

        if (mainWindow.DataContext is MainWindowViewModel viewModel)
        {
            IApplicationUpdateRestartAttachmentService updateRestartAttachmentService =
                GetRequiredService<IApplicationUpdateRestartAttachmentService>();
            updateRestartAttachmentService.Attach(viewModel);
            IDataRootMigrationTargetAttachmentService migrationTargetAttachmentService =
                GetRequiredService<IDataRootMigrationTargetAttachmentService>();
            migrationTargetAttachmentService.Attach(viewModel);
        }

        StartMainWindowInitialization(mainWindow);
        desktopLifetime.ShutdownRequested += OnDesktopLifetimeShutdownRequested;
        desktopLifetime.Exit += OnDesktopLifetimeExit;
        _logger?.LogInformation("Atomic Art main window and desktop lifetime were configured.");
    }

    private static async Task ActivateMainWindowAsync(
        IWindowStateService windowStateService)
    {
        await Dispatcher.UIThread.InvokeAsync(
            windowStateService.ShowAndActivate,
            DispatcherPriority.Send);
    }

    private TService GetRequiredService<TService>()
        where TService : notnull
    {
        if (_serviceProvider is null)
        {
            throw new InvalidOperationException("Desktop service provider has not been created.");
        }

        return _serviceProvider.GetRequiredService<TService>();
    }

    private async Task FlushStateAndShutdownAsync(IClassicDesktopStyleApplicationLifetime desktopLifetime)
    {
        try
        {
            if (_serviceProvider is not null
                && desktopLifetime.MainWindow?.DataContext is IAppStateFlushTarget target)
            {
                IApplicationStateFlushService stateFlushService =
                    _serviceProvider.GetRequiredService<IApplicationStateFlushService>();
                await stateFlushService.FlushAsync(target, CancellationToken.None);
            }

            _isShutdownFlushCompleted = true;
            _logger?.LogInformation("Atomic Art desktop shutdown state flush completed.");
            desktopLifetime.Shutdown();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Atomic Art desktop shutdown state flush failed.");
            _isShutdownFlushCompleted = true;
            desktopLifetime.Shutdown();
        }
        finally
        {
            _isShutdownFlushRunning = false;
        }
    }

    private void OnDesktopLifetimeShutdownRequested(object? sender, ShutdownRequestedEventArgs e)
    {
        if (_isShutdownFlushCompleted)
        {
            return;
        }

        if (_isShutdownFlushRunning)
        {
            e.Cancel = true;
            _logger?.LogDebug("Duplicate desktop shutdown request ignored while state flush is running.");
            return;
        }

        if (sender is not IClassicDesktopStyleApplicationLifetime desktopLifetime)
        {
            return;
        }

        e.Cancel = true;
        _isShutdownFlushRunning = true;
        _logger?.LogInformation("Atomic Art desktop shutdown was requested.");
        _ = FlushStateAndShutdownAsync(desktopLifetime);
    }

    private void OnDesktopLifetimeExit(object? sender, ControlledApplicationLifetimeExitEventArgs e)
    {
        s_singleInstanceCoordinator?.DetachActivationHandler();

        if (_serviceProvider is not null)
        {
            _logger?.LogInformation("Atomic Art desktop process is exiting.");
            _serviceProvider.Dispose();
            _serviceProvider = null;
            _logger = null;
        }
    }
}
