using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using SukiUI.Toasts;

using AtomicArt.Desktop.Controls.Gallery;
using AtomicArt.Desktop.Services;
using AtomicArt.Desktop.Services.Gallery;
using AtomicArt.Desktop.Services.Gallery.State;
using AtomicArt.Desktop.Services.Gallery.Thumbnails;
using AtomicArt.Desktop.Services.Generation;
using AtomicArt.Desktop.Services.Generation.State;
using AtomicArt.Desktop.Services.Logging;
using AtomicArt.Desktop.Services.Paths;
using AtomicArt.Desktop.Services.Settings;
using AtomicArt.Desktop.Services.State;
using AtomicArt.Desktop.Services.Updates;
using AtomicArt.Desktop.ViewModels;
using AtomicArt.Desktop.ViewModels.Gallery;
using AtomicArt.Desktop.ViewModels.Generation;
using AtomicArt.Desktop.ViewModels.Settings;
using AtomicArt.Desktop.ViewModels.Updates;
using AtomicArt.Desktop.Views;
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

        services.AddSingleton<ILoggerProvider, DesktopFileLoggerProvider>();
        services.AddDesktopServicesCore();

        return services;
    }

    public static IServiceCollection AddDesktopServices(
        this IServiceCollection services,
        ILoggerProvider loggerProvider)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(loggerProvider);

        services.AddSingleton(loggerProvider);
        services.AddDesktopServicesCore();

        return services;
    }

    private static void AddDesktopServicesCore(this IServiceCollection services)
    {
        services.AddSingleton<IAtomicArtDataPathProvider, AtomicArtDataPathProvider>();
        services.AddSingleton<DesktopFileLoggingOptions>();
        services.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Debug));
        services.AddShellServices();
        services.AddPlatformServices();
        services.AddGalleryServices();
        services.AddDialogServices();
        services.AddGenerationServices();
        services.AddStateServices();
        services.AddUpdateServices();
    }

    private static IServiceCollection AddShellServices(this IServiceCollection services)
    {
        services.AddTransient<MainWindow>();
        services.AddViewTemplate<GalleryViewModel, GalleryView>();
        services.AddViewTemplate<IModelPanelViewModel, GenerationPanelView>();
        services.AddViewTemplate<SettingsViewModel, SettingsOverlayView>();
        services.AddViewTemplate<ApiBaseAddressSettingViewModel, ApiBaseAddressSettingView>();
        services.AddViewTemplate<SecretSettingViewModel, SecretSettingView>();
        services.AddViewTemplate<ScaleSettingViewModel, ScaleSettingView>();
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
        services.AddSingleton<IStatePathKeyEncoder, StatePathKeyEncoder>();
        services.AddStateSectionsByConvention();
        services.AddSingleton<IStateSectionRegistry, StateSectionRegistry>();
        services.AddSingleton<IAppStateStore, AppStateStore>();
        services.AddSingleton<IStateWriteScheduler, StateWriteScheduler>();
        services.AddSingleton<IAppStateBootstrapper, AppStateBootstrapper>();
        services.AddSingleton<IApplicationStateFlushService, ApplicationStateFlushService>();
        services.AddSettingsStateApplicatorsByConvention();
        services.AddSingleton<IUiScaleSettingValueConverter, UiScaleSettingValueConverter>();
        services.AddSingleton<ISettingsStateService, SettingsStateService>();
        services.AddSingleton<IGenerationPanelStateService, GenerationPanelStateService>();
        services.AddSingleton<IGalleryStateService, GalleryStateService>();
        return services;
    }

    private static IServiceCollection AddPlatformServices(this IServiceCollection services)
    {
        services.AddSingleton<WindowStateService>();
        services.AddSingleton<IWindowStateService>(provider => provider.GetRequiredService<WindowStateService>());
        services.AddSingleton<IWindowAttachmentService>(provider => provider.GetRequiredService<WindowStateService>());
        services.AddSingleton<TrayService>();
        services.AddSingleton<ITrayService>(provider => provider.GetRequiredService<TrayService>());
        services.AddSingleton<ITrayAttachmentService>(provider => provider.GetRequiredService<TrayService>());
        services.AddSingleton<IUiScaleService, UiScaleService>();
        services.AddSettingsDefinitionsByConvention();
        services.AddSettingsItemViewModelFactoriesByConvention();
        services.AddSingleton<ISettingsDefinitionCatalog, SettingsDefinitionCatalog>();
        services.AddTransient<ISettingsItemViewModelProvider, SettingsItemViewModelProvider>();
        services.AddSingleton<IUiThreadDispatcher, AvaloniaUiThreadDispatcher>();
        services.AddSingleton<IViewModelErrorHandler, ViewModelErrorHandler>();
        services.AddSingleton<IApiEndpointService, ApiEndpointService>();
        services.AddSingleton<ISecretStore, ProtectedDesktopSecretStore>();
        services.AddSingleton<IAttachedImageSignatureValidator, AttachedImageSignatureValidator>();
        services.AddSingleton<AttachedImageFileReader>();
        services.AddSingleton<ClipboardImageService>();
        services.AddSingleton<IClipboardImageService>(provider => provider.GetRequiredService<ClipboardImageService>());
        services.AddSingleton<IClipboardAttachmentService>(provider => provider.GetRequiredService<ClipboardImageService>());
        services.AddSingleton<FilePickerService>();
        services.AddSingleton<IFilePickerService>(provider => provider.GetRequiredService<FilePickerService>());
        services.AddSingleton<IFilePickerAttachmentService>(provider => provider.GetRequiredService<FilePickerService>());
        services.AddSingleton<IDragDropImageService, DragDropImageService>();
        services.AddSingleton<ITrustedImageFileService, TrustedImageFileService>();
        services.AddSingleton<IFileRevealService, FileRevealService>();
        services.AddPicaViewer();
        services.AddSingleton<IImageViewerService, ImageViewerService>();
        services.AddSingleton<IUiFrameSchedulerFactory, AvaloniaUiFrameSchedulerFactory>();

        return services;
    }

    private static IServiceCollection AddDialogServices(this IServiceCollection services)
    {
        services.AddSingleton<DialogService>();
        services.AddSingleton<IDialogService>(provider => provider.GetRequiredService<DialogService>());
        services.AddSingleton<IDialogWindowAttachmentService>(provider => provider.GetRequiredService<DialogService>());
        services.AddSingleton<GlobalExceptionService>();

        return services;
    }

    private static IServiceCollection AddGenerationServices(this IServiceCollection services)
    {
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
        services.AddSingleton<GenerationImageContentValidator>();
        services.AddSingleton<IGenerationImageContentValidator>(provider =>
            provider.GetRequiredService<GenerationImageContentValidator>());
        services.AddSingleton<GenerationImageFileNamePolicy>();
        services.AddSingleton<GalleryThumbnailSpecification>();
        services.AddSingleton<GalleryThumbnailImageFormat>();
        services.AddSingleton<IGenerationResultStorage, GenerationResultStorage>();
        services.AddSingleton<IGalleryThumbnailGenerator, GalleryThumbnailGenerator>();
        services.AddSingleton<IGalleryThumbnailStorage, GalleryThumbnailStorage>();
        services.AddSingleton<IPanelAttachmentStore, PanelAttachmentStore>();
        services.AddSingleton<IGenerationLifecycleEventHub, GenerationLifecycleEventHub>();
        services.AddSingleton<IGenerationActivityTracker, GenerationActivityTracker>();
        services.AddSingleton<IGenerationConcurrencyLimiter, GenerationConcurrencyLimiter>();
        services.AddSingleton<AttachedImagePreparationConcurrencyLimiter>();
        services.AddTransient<IAttachedImageCodec, SkiaAttachedImageCodec>();
        services.AddGenerationModelServicesByConvention();
        services.AddGenerationViewModelsByConvention();
        services.AddHttpClient<IGenerationModelCatalogApiClient, GenerationModelCatalogApiClient>();
        services.AddHttpClient<IImageGenerationApiClient, ImageGenerationApiClient>();

        return services;
    }

    private static IServiceCollection AddUpdateServices(this IServiceCollection services)
    {
        services.AddSingleton<ISukiToastManager, SukiToastManager>();
        services.AddSingleton<IApplicationUpdateService, VelopackApplicationUpdateService>();
        services.AddSingleton<ApplicationUpdateRestartCoordinator>();
        services.AddSingleton<IApplicationUpdateRestartCoordinator>(provider =>
            provider.GetRequiredService<ApplicationUpdateRestartCoordinator>());
        services.AddSingleton<IApplicationUpdateRestartAttachmentService>(provider =>
            provider.GetRequiredService<ApplicationUpdateRestartCoordinator>());

        return services;
    }

    private static IServiceCollection AddStateSectionsByConvention(this IServiceCollection services)
    {
        Type markerType = typeof(IStateSection);
        IReadOnlyList<Type> implementationTypes =
            DesktopTypeDiscovery.FindPublicImplementations(markerType);

        foreach (Type implementationType in implementationTypes)
        {
            services.AddSingleton(implementationType);
            services.AddSingleton(markerType, provider =>
                (IStateSection)provider.GetRequiredService(implementationType));
        }

        return services;
    }

    private static IServiceCollection AddGenerationImageFormatsByConvention(this IServiceCollection services)
    {
        Type markerType = typeof(IGenerationImageFormat);
        IReadOnlyList<Type> implementationTypes =
            DesktopTypeDiscovery.FindAllImplementations(markerType);

        foreach (Type implementationType in implementationTypes)
        {
            services.AddSingleton(implementationType);
            services.AddSingleton(markerType, provider =>
                (IGenerationImageFormat)provider.GetRequiredService(implementationType));
        }

        return services;
    }

    private static IServiceCollection AddGenerationItemStatusDescriptorsByConvention(this IServiceCollection services)
    {
        Type markerType = typeof(IRegisteredGenerationItemStatusDescriptor);
        Type descriptorType = typeof(IGenerationItemStatusDescriptor);
        IReadOnlyList<Type> implementationTypes =
            DesktopTypeDiscovery.FindAllImplementations(markerType);

        foreach (Type implementationType in implementationTypes)
        {
            services.AddSingleton(implementationType);
            services.AddSingleton(descriptorType, provider =>
                (IGenerationItemStatusDescriptor)provider.GetRequiredService(implementationType));
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
