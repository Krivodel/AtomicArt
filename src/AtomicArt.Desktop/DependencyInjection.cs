using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using CommunityToolkit.Mvvm.Messaging;
using SukiUI.Toasts;

using AtomicArt.Contracts.Generation;
using AtomicArt.Desktop.Controls.Gallery;
using AtomicArt.Desktop.Services;
using AtomicArt.Desktop.Services.Gallery;
using AtomicArt.Desktop.Services.Gallery.State;
using AtomicArt.Desktop.Services.Gallery.Thumbnails;
using AtomicArt.Desktop.Services.Generation;
using AtomicArt.Desktop.Services.Generation.State;
using AtomicArt.Desktop.Services.Logging;
using AtomicArt.Desktop.Services.Localization;
using AtomicArt.Desktop.Services.Paths;
using AtomicArt.Desktop.Services.Settings;
using AtomicArt.Desktop.Services.State;
using AtomicArt.Desktop.Services.UiAnimation;
using AtomicArt.Desktop.Services.Updates;
using AtomicArt.Desktop.Services.Windows;
using AtomicArt.Desktop.Services.Windowing;
using AtomicArt.Desktop.ViewModels;
using AtomicArt.Desktop.ViewModels.Dialogs;
using AtomicArt.Desktop.ViewModels.Gallery;
using AtomicArt.Desktop.ViewModels.Generation;
using AtomicArt.Desktop.ViewModels.Settings;
using AtomicArt.Desktop.ViewModels.Updates;
using AtomicArt.Desktop.Views;
using AtomicArt.Desktop.Views.Dialogs;
using AtomicArt.Desktop.Views.Gallery;
using AtomicArt.Desktop.Views.Generation;
using AtomicArt.Desktop.Views.Settings;
using AtomicArt.Desktop.Views.Shell;
using AtomicArt.Desktop.Views.Updates;

using Pica.Viewer;

namespace AtomicArt.Desktop;

public static class DependencyInjection
{
    public static IServiceCollection AddDesktopServices(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<AtomicArtDataPathProvider>();
        services.TryAddSingleton<DesktopFileLoggerProvider>();
        services.AddSingleton<ILoggerProvider>(
            provider => provider.GetRequiredService<DesktopFileLoggerProvider>());
        services.AddDesktopServicesCore();

        return services;
    }

    public static IServiceCollection AddDesktopServices(
        this IServiceCollection services,
        AtomicArtDataPathProvider pathProvider,
        DesktopFileLoggerProvider loggerProvider)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(pathProvider);
        ArgumentNullException.ThrowIfNull(loggerProvider);

        services.AddSingleton(pathProvider);
        services.AddSingleton(loggerProvider);
        services.AddSingleton<ILoggerProvider>(loggerProvider);
        services.AddDesktopServicesCore();

        return services;
    }

    private static void AddDesktopServicesCore(this IServiceCollection services)
    {
        services.TryAddSingleton<IMessenger, WeakReferenceMessenger>();
        services.TryAddSingleton<AtomicArtDataPathProvider>();
        services.AddSingleton<IAtomicArtDataPathProvider>(
            provider => provider.GetRequiredService<AtomicArtDataPathProvider>());
        services.AddSingleton<IAtomicArtDataPathSwitcher>(
            provider => provider.GetRequiredService<AtomicArtDataPathProvider>());
        services.AddSingleton<IDataRootLogRelocationService>(
            provider => provider.GetRequiredService<DesktopFileLoggerProvider>());
        services.AddSingleton<DesktopFileLoggingOptions>();
        services.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Debug));
        services.AddStorageConfiguration();
        services.AddDataTransferConfiguration();
        services.AddShellServices();
        services.AddPlatformServices();
        services.AddGalleryServices();
        services.AddDialogServices();
        services.AddGenerationServices();
        services.AddStateServices();
        services.AddUpdateServices();
    }

    private static IServiceCollection AddStorageConfiguration(
        this IServiceCollection services)
    {
        services
            .AddOptions<StorageOptions>()
            .BindConfiguration(StorageOptions.SectionName)
            .Validate(
                StorageOptions.IsValid,
                "Storage configuration must include positive file-size and buffer limits.")
            .ValidateOnStart();
        services.AddSingleton<TrustedFileStreamFactory>();
        services.AddSharedSingletonAliases<LocalizationService>(
            typeof(ILocalizationService),
            typeof(ILocalizationTextProvider));

        return services;
    }

    private static IServiceCollection AddDataTransferConfiguration(
        this IServiceCollection services)
    {
        services
            .AddOptions<DataTransferOptions>()
            .BindConfiguration(DataTransferOptions.SectionName)
            .Validate(
                DataTransferOptions.IsValid,
                "Data-transfer configuration must include positive safety and buffer limits.")
            .ValidateOnStart();

        return services;
    }

    private static IServiceCollection AddShellServices(this IServiceCollection services)
    {
        services.AddTransient<MainWindow>();
        services.AddViewTemplate<GalleryViewModel, GalleryView>();
        services.AddViewTemplate<IModelPanelViewModel, GenerationPanelView>();
        services.AddViewTemplate<SettingsViewModel, SettingsOverlayView>();
        services.AddViewTemplate<ErrorDialogViewModel, ErrorDialogOverlayView>();
        services.AddViewTemplate<DataRootSettingViewModel, DataRootSettingView>();
        services.AddViewTemplate<ApiBaseAddressSettingViewModel, ApiBaseAddressSettingView>();
        services.AddViewTemplate<SecretSettingViewModel, SecretSettingView>();
        services.AddViewTemplate<NumericSettingViewModel, NumericSettingView>();
        services.AddViewTemplate<LanguageSettingViewModel, LanguageSettingView>();
        services.AddViewTemplate<
            GpuResourceCacheSettingViewModel,
            GpuResourceCacheSettingView>();
        services.AddViewTemplate<
            GenerationMetadataViewModel,
            GenerationMetadataOverlayView>();
        services.AddTransient<ApplicationUpdateToastPresenter>();
        services.AddTransient<MainWindowViewModel>();
        services.AddTransient<ApplicationUpdateViewModel>();
        services.AddTransient<SettingsViewModel>();
        services.AddModelPanelViewModelsByConvention();

        return services;
    }

    private static IServiceCollection AddStateServices(this IServiceCollection services)
    {
        services
            .AddOptions<StatePersistenceOptions>()
            .BindConfiguration(StatePersistenceOptions.SectionName)
            .Validate(
                StatePersistenceOptions.IsValid,
                "State configuration must include a positive deferred-write delay.")
            .ValidateOnStart();
        services.AddSingleton<StateWritePolicy>();
        services.AddSingleton<IStatePathKeyEncoder, StatePathKeyEncoder>();
        services.AddStateSectionsByConvention();
        services.AddSingleton<IStateSectionRegistry, StateSectionRegistry>();
        services.AddSingleton<IAppStateStore, AppStateStore>();
        services.AddSingleton<IStateWriteScheduler, StateWriteScheduler>();
        services.AddSingleton<IAppStateBootstrapper, AppStateBootstrapper>();
        services.AddSingleton<IApplicationStateFlushService, ApplicationStateFlushService>();
        services.AddSingleton<IDataRootAccessCoordinator, DataRootAccessCoordinator>();
        services.AddSingleton<AtomicArtDataRootBootstrapStore>();
        services.AddSingleton<DataRootMigrationJournalStore>();
        services.AddSingleton<DataRootMigrationPlanner>();
        services.AddSingleton<DataRootFileTransfer>();
        services.AddSharedSingletonAliases<DataRootMigrationTargetAttachmentService>(
            typeof(IDataRootMigrationTargetAttachmentService));
        services.AddSingleton<
            IAtomicArtDataRootMigrationService,
            AtomicArtDataRootMigrationService>();
        services.AddSettingsStateApplicatorsByConvention();
        services.AddSingleton<IDoubleSettingValueConverter, DoubleSettingValueConverter>();
        services.AddSingleton<ISettingsStateService, SettingsStateService>();
        services.AddSingleton<IGenerationPanelStateService, GenerationPanelStateService>();
        services.AddSingleton<GalleryStatePathConverter>();
        services.AddSingleton<IGalleryStateService, GalleryStateService>();
        services.AddSingleton<
            IGalleryFileOrderSynchronizer,
            GalleryFileOrderSynchronizer>();
        services.AddSingleton<IGalleryStateConsistencyService, GalleryStateConsistencyService>();
        return services;
    }

    private static IServiceCollection AddPlatformServices(this IServiceCollection services)
    {
        services.AddSingleton<WindowPlacementTracker>();
        services.AddSharedSingletonAliases<WindowStateService>(
            typeof(IWindowStateService),
            typeof(IWindowAttachmentService),
            typeof(IWindowPresentationService));
        services.AddSharedSingletonAliases<TrayService>(
            typeof(ITrayService),
            typeof(ITrayAttachmentService));
        services.AddSingleton<IUiScaleService, UiScaleService>();
        services.AddSingleton<IPromptTextSizeService, PromptTextSizeService>();
        services.AddSingleton<IPromptTextSizeController, PromptTextSizeController>();
        services.AddSettingsDefinitionsByConvention();
        services.AddSettingsItemViewModelFactoriesByConvention();
        services.AddSingleton<ISettingsDefinitionCatalog, SettingsDefinitionCatalog>();
        services.AddTransient<ISettingsItemViewModelProvider, SettingsItemViewModelProvider>();
        services.AddSingleton<IUiThreadDispatcher, AvaloniaUiThreadDispatcher>();
        services.AddSingleton<IViewModelErrorHandler, ViewModelErrorHandler>();
        services
            .AddOptions<ApiClientOptions>()
            .BindConfiguration(ApiClientOptions.SectionName)
            .Validate(
                ApiClientOptions.IsValid,
                "API configuration must include positive problem-details limits.")
            .ValidateOnStart();
        services.AddSingleton<IApiEndpointService, ApiEndpointService>();
        services.AddSingleton<ISecretStore, ProtectedDesktopSecretStore>();
        services.AddSingleton<IAttachedImageSignatureValidator, AttachedImageSignatureValidator>();
        services.AddSingleton<AttachedImageFileReader>();
        services.AddSharedSingletonAliases<ClipboardImageService>(
            typeof(IClipboardImageService),
            typeof(IClipboardAttachmentService),
            typeof(ITextClipboardService));
        services.AddSharedSingletonAliases<FilePickerService>(
            typeof(IFilePickerService),
            typeof(IFolderPickerService),
            typeof(IFilePickerAttachmentService));
        services.AddHttpClient<ExternalImageAttachmentReader>((serviceProvider, httpClient) =>
        {
            GenerationClientOptions options = serviceProvider
                .GetRequiredService<IOptions<GenerationClientOptions>>()
                .Value;
            httpClient.Timeout = TimeSpan.FromSeconds(
                options.ExternalImageTimeoutSeconds);
        });
        services.AddSingleton<VirtualFileDropInputSession>();
        services.AddSingleton<IVirtualFileDropInputProvider>(
            provider => provider.GetRequiredService<VirtualFileDropInputSession>());
        services.AddSingleton<WindowsVirtualFileReader>();
        services.AddSingleton<
            IVirtualFileDropAttachmentService,
            WindowsVirtualFileDropAttachmentService>();
        services.AddTransient<IDragDropImageService>(provider =>
            new DragDropImageService(
                provider.GetRequiredService<AttachedImageFileReader>(),
                provider.GetRequiredService<ExternalImageAttachmentReader>(),
                provider.GetRequiredService<IVirtualFileDropInputProvider>(),
                provider.GetRequiredService<ILogger<DragDropImageService>>()));
        services.AddSingleton<ITrustedImageFileService, TrustedImageFileService>();
        services.AddSingleton<IFileRevealService, FileRevealService>();
        services.AddPicaViewer();
        services.AddSingleton<AtomicArtPicaActions>();
        services.AddSingleton<PicaViewerSessionDependencies>();
        services.AddSingleton<PicaViewerSessionFactory>();
        services.AddSharedSingletonAliases<ImageViewerService>(
            typeof(IImageViewerService),
            typeof(IDataRootViewerPreparationService));

        return services;
    }

    private static IServiceCollection AddDialogServices(this IServiceCollection services)
    {
        services.AddSingleton<ErrorDialogViewModel>();
        services.AddSingleton<IDialogService, DialogService>();
        services.AddSingleton<GlobalExceptionService>();

        return services;
    }

    private static IServiceCollection AddGenerationServices(this IServiceCollection services)
    {
        services
            .AddOptions<GenerationClientOptions>()
            .BindConfiguration(GenerationClientOptions.SectionName)
            .Validate(
                GenerationClientOptions.IsValid,
                "Generation configuration must include valid concurrency and retry limits.")
            .ValidateOnStart();
        services.AddSingleton<DesktopModelPanelRegistry>();
        services.AddSingleton<IImageModelOptionCatalog, ImageModelOptionCatalog>();
        services.AddGenerationImageFormatsByConvention();
        services.AddGenerationItemStatusDescriptorsByConvention();
        services.AddSingleton<IGenerationImageFormatRegistry, GenerationImageFormatRegistry>();
        services.AddSingleton<IUnknownGenerationItemStatusDescriptorFactory, UnknownGenerationItemStatusDescriptorFactory>();
        services.AddSingleton<IGenerationItemStatusDescriptorRegistry, GenerationItemStatusDescriptorRegistry>();
        services.AddSingleton<GenerationDurationFormatter>();
        services.AddSingleton<GenerationPriceFormatter>();
        services.AddSingleton<GenerationPricePreviewEstimator>();
        services.AddSingleton<NanoBanana2PanelTextFormatter>();
        services.AddSharedSingletonAliases<GenerationImageContentValidator>(
            typeof(IGenerationImageContentValidator));
        services.AddSingleton<GenerationImageFileNamePolicy>();
        services.AddSingleton<GalleryThumbnailImageFormat>();
        services.AddSingleton<IGenerationResultStorage, GenerationResultStorage>();
        services.AddSingleton<IGenerationStreamingResultStore, GenerationStreamingResultStore>();
        services.AddSingleton<IProviderResponseImageDecoder, JsonBase64ProviderResponseImageDecoder>();
        services.AddSingleton<ProviderResponseImageDecoderRegistry>();
        services.AddSingleton<IGalleryThumbnailGenerator, GalleryThumbnailGenerator>();
        services.AddSingleton<IGalleryThumbnailStorage, GalleryThumbnailStorage>();
        services.AddSingleton<IPanelAttachmentStore, PanelAttachmentStore>();
        services.AddSingleton<IGenerationLifecycleEventHub, GenerationLifecycleEventHub>();
        services.AddSingleton<IGenerationActivityTracker, GenerationActivityTracker>();
        services.AddSingleton<IGenerationAdmissionGate, GenerationAdmissionGate>();
        services.AddSingleton<IGenerationCancellationService, GenerationCancellationService>();
        services.AddSingleton<IGenerationConcurrencyLimiter, GenerationConcurrencyLimiter>();
        services.AddSingleton<AttachedImagePreparationConcurrencyLimiter>();
        services.AddSingleton<AttachedImagePreparationPlanner>();
        services.AddTransient<IAttachedImageCodec, SkiaAttachedImageCodec>();
        services.AddGenerationModelServicesByConvention();
        services.AddGenerationViewModelsByConvention();
        services.AddHttpClient<
            IGenerationModelCatalogApiClient,
            GenerationModelCatalogApiClient>((serviceProvider, httpClient) =>
            {
                ApiClientOptions options = serviceProvider
                    .GetRequiredService<IOptions<ApiClientOptions>>()
                    .Value;
                httpClient.Timeout = TimeSpan.FromSeconds(
                    options.ModelCatalogTimeoutSeconds);
            });
        services.AddHttpClient<IImageGenerationApiClient, ImageGenerationApiClient>(
            (serviceProvider, httpClient) =>
            {
                GenerationClientOptions options = serviceProvider
                    .GetRequiredService<IOptions<GenerationClientOptions>>()
                    .Value;
                httpClient.Timeout = TimeSpan.FromSeconds(
                    options.ProviderResponseTimeoutSeconds);
            });

        return services;
    }

    private static IServiceCollection AddUpdateServices(this IServiceCollection services)
    {
        services
            .AddOptions<ApplicationUpdateOptions>()
            .BindConfiguration(ApplicationUpdateOptions.SectionName)
            .Validate(
                ApplicationUpdateOptions.IsValid,
                "Update configuration must include a positive interval and an HTTPS repository URL.")
            .ValidateOnStart();
        services.AddSingleton<ISukiToastManager, SukiToastManager>();
        services.AddSingleton<IApplicationUpdateService, VelopackApplicationUpdateService>();
        services.AddSharedSingletonAliases<ApplicationUpdateRestartCoordinator>(
            typeof(IApplicationUpdateRestartCoordinator),
            typeof(IApplicationUpdateRestartAttachmentService));

        return services;
    }

    private static IServiceCollection AddStateSectionsByConvention(this IServiceCollection services)
    {
        return services.AddSharedSingletonImplementationsByConvention(
            typeof(IStateSection),
            typeof(IStateSection),
            type => DesktopTypeDiscovery.FindPublicImplementations(type));
    }

    private static IServiceCollection AddGenerationImageFormatsByConvention(this IServiceCollection services)
    {
        return services.AddSharedSingletonImplementationsByConvention(
            typeof(IGenerationImageFormat),
            typeof(IGenerationImageFormat),
            type => DesktopTypeDiscovery.FindAllImplementations(type));
    }

    private static IServiceCollection AddGenerationItemStatusDescriptorsByConvention(this IServiceCollection services)
    {
        return services.AddSharedSingletonImplementationsByConvention(
            typeof(IGenerationItemStatusDescriptor),
            typeof(IRegisteredGenerationItemStatusDescriptor),
            type => DesktopTypeDiscovery.FindAllImplementations(type));
    }

    private static IServiceCollection AddSharedSingletonImplementationsByConvention(
        this IServiceCollection services,
        Type serviceType,
        Type markerType,
        Func<Type, IEnumerable<Type>> findImplementationTypes)
    {
        ArgumentNullException.ThrowIfNull(findImplementationTypes);

        IReadOnlyList<Type> implementationTypes = findImplementationTypes(markerType).ToList();

        foreach (Type implementationType in implementationTypes)
        {
            services.AddSharedSingletonImplementation(serviceType, implementationType);
        }

        return services;
    }

    private static IServiceCollection AddModelPanelViewModelsByConvention(this IServiceCollection services)
    {
        Type panelType = typeof(IModelPanelViewModel);
        IReadOnlyList<Type> panelTypes =
            DesktopTypeDiscovery.FindPublicImplementations(panelType);

        foreach (Type panel in panelTypes)
        {
            services.AddTransient(panel);
            services.AddTransient(panelType, provider =>
                (IModelPanelViewModel)provider.GetRequiredService(panel));
        }

        return services;
    }

    private static IServiceCollection AddGenerationModelServicesByConvention(this IServiceCollection services)
    {
        Type markerType = typeof(IGenerationModelService);
        IReadOnlyList<Type> implementationTypes =
            DesktopTypeDiscovery.FindPublicImplementations(markerType);

        foreach (Type implementationType in implementationTypes)
        {
            services.AddTransient(implementationType);
            AddMatchingInterfaceRegistration(services, implementationType, markerType);
        }

        return services;
    }

    private static IServiceCollection AddGenerationViewModelsByConvention(this IServiceCollection services)
    {
        Type markerType = typeof(IGenerationModelViewModel);
        IReadOnlyList<Type> viewModelTypes =
            DesktopTypeDiscovery.FindPublicImplementations(markerType);

        foreach (Type viewModelType in viewModelTypes)
        {
            services.AddTransient(viewModelType);
        }

        return services;
    }

    private static void AddMatchingInterfaceRegistration(
        IServiceCollection services,
        Type implementationType,
        Type excludedInterfaceType)
    {
        Type? interfaceType = implementationType
            .GetInterfaces()
            .FirstOrDefault(candidateInterfaceType => candidateInterfaceType != excludedInterfaceType
                && candidateInterfaceType != typeof(IDisposable)
                && candidateInterfaceType.Name == $"I{implementationType.Name}");

        if (interfaceType is not null)
        {
            services.AddTransient(interfaceType, provider => provider.GetRequiredService(implementationType));
        }
    }

}
